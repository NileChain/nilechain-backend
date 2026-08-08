FROM python:3.12-slim

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends build-essential \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app ./app

ENV CHROMA_MODE=persistent \
    CHROMA_PERSIST_DIR=/data/chroma \
    ALLOW_SEED=false \
    LOG_LEVEL=INFO \
    PORT=8001

EXPOSE 8001

# Railway/Render inject PORT; local default 8001 matches NileChain ChromaService.
CMD ["sh", "-c", "uvicorn app.main:app --host 0.0.0.0 --port ${PORT:-8001}"]
