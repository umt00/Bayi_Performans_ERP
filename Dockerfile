FROM python:3.10-slim

WORKDIR /app

# curl yalnız gerektiğinde container içi tanılama/healthcheck için tutulur.
# psycopg2-binary hazır wheel kullandığı için derleyici ve libpq-dev gerekmez.
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Yalnız production FastAPI bağımlılıklarını kur. Test/veri bilimi paketleri
# ayrı geliştirme ortamında requirements.txt üzerinden yüklenir.
COPY requirements.fastapi.txt /app/
RUN pip install --no-cache-dir -r requirements.fastapi.txt

# Yalnız FastAPI'nin kullandığı uygulama kodlarını kopyala. .NET/UI çıktıları
# ayrı servislerdir ve bu imajın build context'ine girmez.
COPY app/__init__.py /app/app/__init__.py
COPY app/ai/ /app/app/ai/
COPY fast_api/ /app/fast_api/
COPY config/ /app/config/

ENV PYTHONPATH=/app
ENV PORT=8000

EXPOSE 8000

# Production Uvicorn komutu
CMD ["sh", "-c", "uvicorn fast_api.main:app --host 0.0.0.0 --port ${PORT:-8000}"]
