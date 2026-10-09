from datetime import datetime, timedelta
import os
from airflow import DAG
from airflow.operators.bash import BashOperator

default_args = {
    'owner': 'penta',
    'depends_on_past': False,
    'email_on_failure': False,
    'email_on_retry': False,
    'retries': 1,
    'retry_delay': timedelta(minutes=5),
}

# Airflow container içindeki proje kök dizini (Genelde /opt/airflow olur)
project_root = os.environ.get('AIRFLOW_HOME', '/opt/airflow')
script_path = os.path.join(project_root, 'elastic_migration', 'sync_to_elastic.py')

with DAG(
    dag_id='sync_elastic_dag',
    default_args=default_args,
    description='Supabase verilerini Elasticsearch e yığınlar halinde Upsert eder.',
    schedule_interval='45 10 * * *', # Günlük 10:45'te veya gunluk_satis_etl_dag tarafından tetiklenerek çalışır
    start_date=datetime(2026, 1, 1),
    catchup=False,
    tags=['ETL', 'Elasticsearch', 'Sync']
) as dag:

    # sync_to_elastic.py scriptini çalıştıran task
    run_elastic_sync = BashOperator(
        task_id='run_sync_to_elastic',
        bash_command=f'python "{script_path}"'
    )
