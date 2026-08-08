from __future__ import annotations

import logging
from typing import Any

import chromadb
from chromadb.api import ClientAPI

from app.config import Settings

logger = logging.getLogger("chroma_proxy.backend")


def create_chroma_client(settings: Settings) -> ClientAPI:
    mode = settings.chroma_mode
    logger.info("Initializing Chroma client mode=%s", mode)

    if mode == "cloud":
        if not settings.chroma_api_key:
            raise RuntimeError(
                "CHROMA_MODE=cloud requires CHROMA_API_KEY "
                "(and usually CHROMA_TENANT + CHROMA_DATABASE)."
            )
        kwargs: dict[str, Any] = {"api_key": settings.chroma_api_key}
        if settings.chroma_tenant:
            kwargs["tenant"] = settings.chroma_tenant
        if settings.chroma_database:
            kwargs["database"] = settings.chroma_database
        if settings.chroma_host:
            kwargs["cloud_host"] = settings.chroma_host
            kwargs["cloud_port"] = 443
        return chromadb.CloudClient(**kwargs)

    if mode == "http":
        return chromadb.HttpClient(
            host=settings.chroma_server_host,
            port=settings.chroma_server_port,
            ssl=settings.chroma_server_ssl,
        )

    if mode == "persistent":
        return chromadb.PersistentClient(path=settings.chroma_persist_dir)

    raise RuntimeError(f"Unsupported CHROMA_MODE: {mode}")


def get_or_create_collection(client: ClientAPI, collection_name: str):
    # Default embedding function (client-side) embeds documents / query_texts.
    # NileChain C# does not generate embeddings; the proxy owns that step.
    return client.get_or_create_collection(name=collection_name)
