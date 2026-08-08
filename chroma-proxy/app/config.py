from functools import lru_cache
from typing import Literal

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
        populate_by_name=True,
    )

    # cloud | persistent | http
    chroma_mode: Literal["cloud", "persistent", "http"] = "persistent"

    # Chroma Cloud (CHROMA_* matches official CloudClient env names)
    chroma_api_key: str | None = None
    chroma_tenant: str | None = None
    chroma_database: str | None = None
    chroma_host: str | None = None  # optional non-default cloud region host

    # PersistentClient (local / single-process demo)
    chroma_persist_dir: str = "./.chroma-data"

    # HttpClient → self-hosted chromadb/chroma server
    chroma_server_host: str = "localhost"
    chroma_server_port: int = 8000
    chroma_server_ssl: bool = False

    # Optional shared secret: if set, require X-NileChain-Proxy-Key
    proxy_api_key: str | None = None

    request_timeout_seconds: float = Field(default=60.0, gt=0)
    log_level: str = "INFO"
    # Host platforms inject PORT; default 8001 matches NileChain ChromaService.
    port: int = Field(default=8001, validation_alias="PORT")

    # When true, POST /seed upserts the NileChain knowledge docs (demo only)
    allow_seed: bool = False


@lru_cache
def get_settings() -> Settings:
    return Settings()
