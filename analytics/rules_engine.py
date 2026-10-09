import json
import pandas as pd
from pathlib import Path

class RulesEngine:
    def __init__(self):
        self.base_dir = Path(__file__).resolve().parent
        self.data_dir = (self.base_dir / ".." / "data" / "processed" / "csv").resolve()
        
        # Load thresholds
        config_path = self.base_dir / ".." / "config" / "thresholds.json"
        with open(config_path, "r", encoding="utf-8") as f:
            self.thresholds = json.load(f)
            
        self.marj_esigi_pct = self.thresholds.get("marj_esigi_pct", 0.15)
        self.max_iskonto_pct = self.thresholds.get("max_iskonto_pct", 0.15)
        self.hedef_risk_esigi_pct = self.thresholds.get("hedef_risk_esigi_pct", 0.80)
        self.stok_gun_esigi = self.thresholds.get("stok_gun_esigi", 7)
        self.min_marj_red_pct = self.thresholds.get("min_marj_red_pct", 0.0)
        self.iade_risk_esigi_pct = self.thresholds.get("iade_risk_esigi_pct", 0.10)
        self.risk_weights = self.thresholds.get("risk_weights", {})
        self.risk_levels = self.thresholds.get("risk_levels", {})
        
        # Load data for evaluation
        try:
            self.satis_df = pd.read_csv(self.data_dir / "temiz_satis.csv")
            self.urunler_df = pd.read_csv(self.data_dir / "temiz_urunler.csv")
            self.stok_df = pd.read_csv(self.data_dir / "temiz_stok.csv")
            self.hedef_df = pd.read_csv(self.data_dir / "temiz_hedefler.csv")
        except FileNotFoundError:
            self.satis_df = pd.DataFrame()
            self.urunler_df = pd.DataFrame()
            self.stok_df = pd.DataFrame()
            self.hedef_df = pd.DataFrame()
            
    def _calculate_depo_dynamic_stock_threshold(self, urun_kodu, depo_kodu=None, current_date=None):
        """
        Şirket depolarındaki ürünlerin toplam çıkış hızına göre dinamik stok eşiğini (7 günlük ihtiyaç) hesaplar.
        """
        if current_date is None:
            if not self.satis_df.empty:
                current_date = pd.to_datetime(self.satis_df['Tarih']).max()
            else:
                return 5
        else:
            current_date = pd.to_datetime(current_date)
            
        start_date = current_date - pd.Timedelta(days=90)
        df_sales = self.satis_df[(self.satis_df['UrunKodu'] == urun_kodu)].copy()
        if depo_kodu and 'DepoKodu' in df_sales.columns:
            df_sales = df_sales[df_sales['DepoKodu'] == depo_kodu]
            
        if df_sales.empty:
            return 5
            
        df_sales['Tarih'] = pd.to_datetime(df_sales['Tarih'])
        recent_sales = df_sales[(df_sales['Tarih'] > start_date) & (df_sales['Tarih'] <= current_date)]
        
        total_quantity = recent_sales['Miktar'].sum() if not recent_sales.empty else 0
        daily_average = total_quantity / 90.0
        
        threshold = daily_average * self.stok_gun_esigi
        return max(threshold, 1.0)
        
    def evaluate_proposal(self, request: dict) -> dict:
        """
        Teklif değerlendirme kural motoru:
        - Gizli maliyet ile zararına satış kontrolü
        - Depo stok yeterlilik kontrolü
        - Hedef gerçekleşme ve marj kontrolleri
        """
        bayi_kodu = request.get("bayi_kodu")
        urun_kodu = request.get("urun_kodu")
        miktar = request.get("miktar", 0)
        birim_fiyat = request.get("birim_fiyat", 0)
        iskonto_orani = request.get("iskonto_orani", 0.0)
        depo_kodu = request.get("depo_kodu", "DP01")
        
        # 1. Ürün Maliyeti ve Marj Hesabı
        product_info = self.urunler_df[self.urunler_df['UrunKodu'] == urun_kodu]
        if product_info.empty:
            return {"karar": "REJECTED", "gerekce_kodlari": ["VERI_EKSIK_MALIYET"], "ai_comment": "Ürün maliyet kaydı bulunamadı."}
            
        birim_maliyet = product_info.iloc[0]['BirimMaliyet']
        net_fiyat = birim_fiyat * (1.0 - iskonto_orani)
        marj = (net_fiyat - birim_maliyet) / net_fiyat if net_fiyat > 0 else -1.0
            
        # 2. Seçilen Depodaki Stok Kontrolü
        stock_info = self.stok_df[(self.stok_df['UrunKodu'] == urun_kodu) & (self.stok_df['DepoKodu'] == depo_kodu)]
        mevcut_stok = stock_info['MevcutStok'].sum() if not stock_info.empty else 0
        dynamic_threshold = self._calculate_depo_dynamic_stock_threshold(urun_kodu, depo_kodu)
        
        # 3. İade Oranı
        product_sales = self.satis_df[self.satis_df['UrunKodu'] == urun_kodu]
        if not product_sales.empty:
            total_sold = product_sales[product_sales['Miktar'] > 0]['Miktar'].sum()
            total_returned = abs(product_sales[product_sales['Miktar'] < 0]['Miktar'].sum())
            iade_orani = total_returned / total_sold if total_sold > 0 else 0.0
        else:
            iade_orani = 0.0
            
        # 4. Hedef Gerçekleşme Oranı
        hedef_tutturma_orani = 1.0
        gerceklesen_ciro = 0
        aylik_hedef = 0
        
        if not self.satis_df.empty:
            current_date = pd.to_datetime(self.satis_df['Tarih']).max()
            current_month = current_date.month
            current_year = current_date.year
            
            df_bayi = self.satis_df[self.satis_df['BayiKodu'] == bayi_kodu].copy()
            df_bayi['Tarih'] = pd.to_datetime(df_bayi['Tarih'])
            df_bayi_month = df_bayi[(df_bayi['Tarih'].dt.month == current_month) & (df_bayi['Tarih'].dt.year == current_year)]
            
            if not df_bayi_month.empty:
                gerceklesen_ciro = (df_bayi_month['Miktar'] * df_bayi_month['BirimFiyat'] * (1 - df_bayi_month['IskontoOrani'])).sum()
                
            if not self.hedef_df.empty:
                hedef_info = self.hedef_df[(self.hedef_df['BayiKodu'] == bayi_kodu) & (self.hedef_df['Ay'] == current_month) & (self.hedef_df['Yil'] == current_year)]
                if not hedef_info.empty:
                    aylik_hedef = hedef_info['Hedef'].sum()
            
            if aylik_hedef > 0:
                hedef_tutturma_orani = gerceklesen_ciro / aylik_hedef

        # 5. Gerekçe Kodları
        gerekce_kodlari = []
        is_zararina = False
        
        if net_fiyat < birim_maliyet or marj < 0:
            gerekce_kodlari.append("ZARARINA_SATIS")
            is_zararina = True
        elif marj < self.marj_esigi_pct:
            gerekce_kodlari.append("MARJ_ESIK_ALTI")
            
        if iskonto_orani > self.max_iskonto_pct:
            gerekce_kodlari.append("YUKSEK_ISKONTO")
            
        if mevcut_stok < miktar:
            gerekce_kodlari.append("STOK_YETERSIZ")
        elif mevcut_stok < dynamic_threshold:
            gerekce_kodlari.append("KRITIK_DEPO_STOK")
            
        if iade_orani > self.iade_risk_esigi_pct:
            gerekce_kodlari.append("YUKSEK_IADE")
            
        if hedef_tutturma_orani < self.hedef_risk_esigi_pct:
            gerekce_kodlari.append("HEDEF_ALTINDA")
            
        # 6. Risk Skoru
        risk_skoru = sum([self.risk_weights.get(code, 20) for code in gerekce_kodlari])
        
        if risk_skoru <= self.risk_levels.get("DUSUK_max", 30):
            risk_seviyesi = "DUSUK"
        elif risk_skoru <= self.risk_levels.get("ORTA_max", 60):
            risk_seviyesi = "ORTA"
        else:
            risk_seviyesi = "YUKSEK"
            
        # 7. Karar Mantığı
        if is_zararina or marj < self.min_marj_red_pct or marj < 0.05:
            karar = "REJECTED"
        elif risk_seviyesi == "DUSUK" and not gerekce_kodlari:
            karar = "APPROVED"
        else:
            karar = "NEED_APPROVAL"
            
        return {
            "karar": karar,
            "risk_skoru": risk_skoru,
            "risk_seviyesi": risk_seviyesi,
            "gerekce_kodlari": gerekce_kodlari,
            "hesaplanan_marj_pct": round(marj * 100, 1),
            "marj_esigi_pct": round(self.marj_esigi_pct * 100, 1),
            "mevcut_depo_stok": int(mevcut_stok),
            "depo_kodu": depo_kodu,
            "hedef_durumu": f"Hedef: {aylik_hedef:,.2f} TL | Ciro: {gerceklesen_ciro:,.2f} TL | Oran: %{round(hedef_tutturma_orani*100, 1)}",
            "ai_comment": f"Teklif marjı %{round(marj * 100, 1)} ve iskonto %{round(iskonto_orani * 100, 1)}. Seçilen {depo_kodu} deposundaki mevcut stok: {int(mevcut_stok)} adet. Risk Skoru: {risk_skoru} ({risk_seviyesi}).",
            "comment_source": "rule_engine"
        }

if __name__ == "__main__":
    engine = RulesEngine()
    test_req = {
      "bayi_kodu": "B0012", 
      "urun_kodu": "U1001", 
      "depo_kodu": "DP01", 
      "miktar": 8, 
      "birim_fiyat": 35000.00, 
      "iskonto_orani": 0.05
    }
    print("Testing rule engine:")
    print(json.dumps(engine.evaluate_proposal(test_req), indent=2, ensure_ascii=False))
