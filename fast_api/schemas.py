from pydantic import BaseModel, Field
from typing import List, Optional

class KalemCreate(BaseModel):
    sira_no: Optional[int] = 1
    urun_kodu: str
    urun_adi: Optional[str] = None
    depo_kodu: Optional[str] = None  # Backend bayi sehir/bolgesine gore otomatik atar
    depo_adi: Optional[str] = None
    miktar: float = Field(gt=0, description="Sipariş miktarı 0'dan büyük olmalıdır")
    birim_fiyat: float = Field(ge=0, description="Birim fiyat 0 veya daha büyük olmalıdır")
    iskonto_orani: Optional[float] = Field(default=0.0, ge=0, le=1, description="İskonto oranı 0 ile 1 arasında olmalıdır")

class TeklifCreate(BaseModel):
    teklif_id: Optional[str] = None
    bayi_kodu: str
    aciklama: Optional[str] = None
    kalemler: List[KalemCreate]

class TeklifDurumUpdate(BaseModel):
    durum: str

