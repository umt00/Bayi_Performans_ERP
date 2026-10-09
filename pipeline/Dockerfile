FROM apache/airflow:2.7.1
USER root
RUN apt-get update \
  && apt-get install -y --no-install-recommends \
         build-essential \
         freetds-dev \
         libssl-dev \
         libffi-dev \
  && apt-get autoremove -yqq --purge \
  && apt-get clean \
  && rm -rf /var/lib/apt/lists/*
USER airflow
RUN pip install --upgrade pip && pip install --no-cache-dir pymssql==2.2.8 scikit-learn pandas numpy sqlalchemy psycopg2-binary openpyxl elasticsearch
