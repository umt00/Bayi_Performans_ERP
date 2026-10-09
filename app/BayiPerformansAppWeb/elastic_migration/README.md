# Elasticsearch yerel altyapısı

Bu klasör, `Elasticsearch backend yönerge.docx` belgesindeki yerel arama
altyapısının bu repoda doğrulanabilen kısmını içerir.

- `index_mapping.json`, `satislar_index` için Türkçe küçük harf, ASCII folding ve
  3-5 karakter N-Gram analyzer ayarlarını tanımlar.
- Kök `docker-compose.yml` içindeki `elasticsearch-init` servisi index yoksa
  bu mapping ile oluşturur; var olan indexi silmez veya yeniden yaratmaz.
- Elasticsearch `http://localhost:9200`, Kibana `http://localhost:5601`
  adresinden açılır.
- Docker ağı açıkça `elastic_migration_elastic_net` adıyla oluşturulur. Airflow
  ayrı bir Compose projesindeyse bu ağa `external: true` ile bağlanabilir.

## Kapsam sınırı

Supabase verisini Elasticsearch'e taşıyan `sync_to_elastic.py` ve bu işlemi
tetikleyen Airflow DAG'ı Data/ETL-Automation ekibinin teslimatıdır. Bu klasör
veriyi kopyalamaz; yalnız yerel Elasticsearch/Kibana altyapısını ve ortak index
sözleşmesini hazırlar. Mapping alanları veri ekibiyle dondurulmadan production
indexi olarak kabul edilmemelidir.

Yerel geliştirmede güvenlik bilerek kapalıdır. Bu ayar internete açık veya
production ortamında kullanılmamalıdır.
