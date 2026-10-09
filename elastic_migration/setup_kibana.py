import requests
import json
import time

KIBANA_URL = "http://localhost:5601"
DATA_VIEW_API = f"{KIBANA_URL}/api/data_views/data_view"
INDEX_PATTERN = "satis_index"
TIME_FIELD = "Tarih"

def setup_kibana_data_view():
    print(f"Kibana bekleniyor ({KIBANA_URL})...")
    
    # Kibana'nın ayağa kalkmasını bekle
    max_retries = 30
    for i in range(max_retries):
        try:
            response = requests.get(f"{KIBANA_URL}/api/status", headers={"kbn-xsrf": "true"})
            if response.status_code == 200:
                print("Kibana ayakta ve hazır!")
                break
        except requests.ConnectionError:
            pass
        print(f"Kibana'ya ulaşılamadı. Yeniden deneniyor... ({i+1}/{max_retries})")
        time.sleep(5)
    else:
        print("Kibana başlatılamadı veya ulaşılamıyor.")
        return

    # Data View (Index Pattern) oluşturma isteği
    headers = {
        "kbn-xsrf": "true",
        "Content-Type": "application/json"
    }
    
    payload = {
        "data_view": {
            "title": INDEX_PATTERN,
            "name": "Satışlar Data View",
            "timeFieldName": TIME_FIELD
        }
    }
    
    print(f"'{INDEX_PATTERN}' için Data View oluşturuluyor...")
    response = requests.post(DATA_VIEW_API, headers=headers, json=payload)
    
    if response.status_code == 200:
        print(f"[OK] Data View başarıyla oluşturuldu! Artık Discover sekmesinden verileri inceleyebilirsiniz.")
    elif response.status_code == 409: # Conflict (Zaten varsa)
        print(f"[OK] Data View zaten mevcut.")
    else:
        print(f"[HATA] Data View oluşturulamadı: {response.status_code} - {response.text}")

if __name__ == "__main__":
    setup_kibana_data_view()
