from airflow import DAG
from airflow.providers.docker.operators.docker import DockerOperator
from datetime import datetime, timedelta

default_args = {
    'owner': 'ai_team',
    'depends_on_past': False,
    'email_on_failure': True,
    'email_on_retry': False,
    'retries': 1,
    'retry_delay': timedelta(minutes=5),
}

# Not: Bu örnek bir DAG şablonudur. 
# Şirketin Airflow altyapısına göre 'image' ve 'network_mode' parametrelerinin 
# DevOps / Veri Mühendisliği ekibi tarafından güncellenmesi beklenmektedir.
with DAG(
    'dealer_ai_evaluation_monthly',
    default_args=default_args,
    description='Her ayın 1inde bayi risk ve performans AI değerlendirmesini (Docker ile) çalıştırır',
    schedule_interval='0 8 1 * *', # Her ayın 1'inde sabah saat 08:00
    start_date=datetime(2026, 1, 1),
    catchup=False,
    tags=['ai', 'dealer_risk', 'automation'],
) as dag:

    # Docker konteynerimizi çalıştıracak operatör
    run_ai_evaluation = DockerOperator(
        task_id='run_ai_evaluation_task',
        # image='sirket_registry/penta-intern-ai:latest', # DevOps ekibi burayı dolduracak
        image='penta-intern-ai:latest', 
        container_name='ai_eval_worker',
        api_version='auto',
        auto_remove=True,
        command='python app/ai/ai_demo.py',
        docker_url='unix://var/run/docker.sock',
        network_mode='bridge'
    )

    run_ai_evaluation
