# NileChain Chroma Proxy
#
# Preserves the HTTP contract expected by NileChain.AI/RAG/ChromaService.cs:
#   POST /query, POST /add, POST /upsert  (+ GET /health, optional POST /seed)
#
# Architecture:
#   NileChain API  →  this proxy (HTTPS)  →  Chroma Cloud | Persistent | HttpClient
#
# Embeddings:
#   NileChain sends raw documents / query_texts only.
#   This proxy uses chromadb's default embedding function (client-side).
#   The original localhost:8001 proxy source is not in the repo; its EF is unknown.
#   We intentionally use Chroma's default rather than inventing a second system.
#
# Recommended production:
#   CHROMA_MODE=cloud + Railway (or similar) for this service
#   Heroku: Chroma__BaseUrl=https://<this-service-public-url>
#
# Local run:
#   python -m venv .venv
#   .venv\Scripts\activate   (Windows)
#   pip install -r requirements.txt
#   copy .env.example .env
#   uvicorn app.main:app --host 0.0.0.0 --port 8001
#
# Seed (idempotent upsert of 33 docs):
#   set ALLOW_SEED=true
#   curl -X POST http://localhost:8001/seed
#
# Deploy (Railway example):
#   Root directory: backend/chroma-proxy
#   Build: Docker (Dockerfile) or Nixpacks (pip install -r requirements.txt)
#   Start: uvicorn app.main:app --host 0.0.0.0 --port $PORT
#   Env: CHROMA_MODE=cloud, CHROMA_API_KEY, CHROMA_TENANT, CHROMA_DATABASE
#   Optional: ALLOW_SEED=true once, POST /seed, then set ALLOW_SEED=false
#
# Do not commit secrets. Do not log API keys.
