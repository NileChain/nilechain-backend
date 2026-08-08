from __future__ import annotations

import logging
import secrets
import time
from contextlib import asynccontextmanager
from typing import Any

from fastapi import Depends, FastAPI, Header, HTTPException, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field

from app.chroma_backend import create_chroma_client, get_or_create_collection
from app.config import Settings, get_settings
from app.seed_data import DOCUMENT_COUNT, build_seed_payload

logger = logging.getLogger("chroma_proxy")


class QueryRequest(BaseModel):
    collection_name: str = Field(..., min_length=1)
    query_texts: list[str] = Field(..., min_length=1)
    n_results: int = Field(default=3, ge=1, le=50)


class UpsertRequest(BaseModel):
    collection_name: str = Field(..., min_length=1)
    ids: list[str] = Field(..., min_length=1)
    documents: list[str] = Field(..., min_length=1)
    metadatas: list[dict[str, Any]] | None = None


def _configure_logging(level: str) -> None:
    logging.basicConfig(
        level=getattr(logging, level.upper(), logging.INFO),
        format="%(asctime)s %(levelname)s [%(name)s] %(message)s",
    )


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = get_settings()
    _configure_logging(settings.log_level)
    app.state.settings = settings
    app.state.chroma = create_chroma_client(settings)
    # Warm default collection so first NileChain call is not cold-create only.
    get_or_create_collection(app.state.chroma, "nilechain_knowledge")
    logger.info(
        "Chroma proxy ready mode=%s collection=nilechain_knowledge",
        settings.chroma_mode,
    )
    yield


app = FastAPI(
    title="NileChain Chroma Proxy",
    version="1.0.0",
    lifespan=lifespan,
)


def require_proxy_key(
    settings: Settings = Depends(get_settings),
    x_nilechain_proxy_key: str | None = Header(default=None),
) -> None:
    expected = settings.proxy_api_key
    if not expected:
        return
    if not x_nilechain_proxy_key or not secrets.compare_digest(
        x_nilechain_proxy_key, expected
    ):
        raise HTTPException(status_code=401, detail="Invalid or missing proxy key")


@app.middleware("http")
async def request_timing(request: Request, call_next):
    started = time.perf_counter()
    response = await call_next(request)
    elapsed_ms = (time.perf_counter() - started) * 1000
    logger.info(
        "method=%s path=%s status=%s duration_ms=%.1f",
        request.method,
        request.url.path,
        response.status_code,
        elapsed_ms,
    )
    return response


@app.get("/health")
def health(settings: Settings = Depends(get_settings)) -> dict[str, Any]:
    try:
        heartbeat = app.state.chroma.heartbeat()
        return {
            "status": "ok",
            "mode": settings.chroma_mode,
            "heartbeat": heartbeat,
            "default_collection": "nilechain_knowledge",
        }
    except Exception as exc:  # noqa: BLE001
        logger.exception("Health check failed")
        raise HTTPException(status_code=503, detail=f"Chroma unavailable: {exc}") from exc


@app.post("/query", dependencies=[Depends(require_proxy_key)])
def query(body: QueryRequest) -> dict[str, Any]:
    try:
        collection = get_or_create_collection(app.state.chroma, body.collection_name)
        result = collection.query(
            query_texts=body.query_texts,
            n_results=body.n_results,
            include=["documents"],
        )
        documents = result.get("documents") or [[]]
        return {"documents": documents}
    except HTTPException:
        raise
    except Exception as exc:  # noqa: BLE001
        logger.exception(
            "query failed collection=%s n_results=%s",
            body.collection_name,
            body.n_results,
        )
        raise HTTPException(status_code=502, detail=f"Chroma query failed: {exc}") from exc


def _ingest(body: UpsertRequest, *, use_upsert: bool) -> dict[str, Any]:
    if len(body.ids) != len(body.documents):
        raise HTTPException(
            status_code=400,
            detail="ids and documents must have the same length",
        )
    metadatas = body.metadatas
    if metadatas is not None and len(metadatas) != len(body.ids):
        raise HTTPException(
            status_code=400,
            detail="metadatas length must match ids when provided",
        )

    try:
        collection = get_or_create_collection(app.state.chroma, body.collection_name)
        kwargs: dict[str, Any] = {
            "ids": body.ids,
            "documents": body.documents,
        }
        if metadatas is not None:
            kwargs["metadatas"] = metadatas

        if use_upsert:
            collection.upsert(**kwargs)
            action = "upsert"
        else:
            collection.add(**kwargs)
            action = "add"

        logger.info(
            "%s ok collection=%s count=%s",
            action,
            body.collection_name,
            len(body.ids),
        )
        return {
            "status": "ok",
            "action": action,
            "collection_name": body.collection_name,
            "count": len(body.ids),
        }
    except HTTPException:
        raise
    except Exception as exc:  # noqa: BLE001
        # NileChain tries /add then /upsert; return 409-ish for duplicate add.
        message = str(exc)
        logger.exception(
            "%s failed collection=%s count=%s",
            "upsert" if use_upsert else "add",
            body.collection_name,
            len(body.ids),
        )
        status = 409 if (not use_upsert and "exist" in message.lower()) else 502
        raise HTTPException(status_code=status, detail=message) from exc


@app.post("/add", dependencies=[Depends(require_proxy_key)])
def add_documents(body: UpsertRequest) -> dict[str, Any]:
    return _ingest(body, use_upsert=False)


@app.post("/upsert", dependencies=[Depends(require_proxy_key)])
def upsert_documents(body: UpsertRequest) -> dict[str, Any]:
    return _ingest(body, use_upsert=True)


@app.post("/seed", dependencies=[Depends(require_proxy_key)])
def seed(settings: Settings = Depends(get_settings)) -> dict[str, Any]:
    """Idempotent upsert of the 33 NileChain knowledge documents (ALLOW_SEED=true)."""
    if not settings.allow_seed:
        raise HTTPException(
            status_code=403,
            detail="Seeding disabled. Set ALLOW_SEED=true to enable.",
        )
    payload = build_seed_payload("nilechain_knowledge")
    result = _ingest(UpsertRequest(**payload), use_upsert=True)
    result["expected_documents"] = DOCUMENT_COUNT
    return result


@app.exception_handler(Exception)
async def unhandled(request: Request, exc: Exception):
    logger.exception("Unhandled error path=%s", request.url.path)
    return JSONResponse(status_code=500, content={"detail": "Internal proxy error"})
