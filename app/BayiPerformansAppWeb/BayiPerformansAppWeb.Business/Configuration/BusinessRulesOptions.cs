using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.Generic;

namespace BayiPerformansAppWeb.Business.Configuration;

/// <summary>
/// Business katmanındaki karar mekanizmalarının kullandığı
/// bütün eşik ve ağırlık değerlerini temsil eder.
///
/// Bu değerler appsettings.json veya thresholds.json gibi
/// configuration kaynaklarından bind edilebilir.
///
/// Oranlar 0-1 formatında tutulur.
///
/// Örneğin:
/// %15 = 0.15
/// %80 = 0.80
/// </summary>
public sealed class BusinessRulesOptions
{
    /// <summary>
    /// Bir bayinin marjının altında kalması durumunda
    /// risk gerekçesi oluşturulacak marj eşiğidir.
    ///
    /// Örnek:
    /// 0.15 = %15
    /// </summary>
    public decimal MarjEsigiPct { get; set; }

    /// <summary>
    /// Tekliflerde veya satış performansında
    /// yüksek iskonto kabul edilen maksimum iskonto oranıdır.
    ///
    /// Örnek:
    /// 0.15 = %15
    /// </summary>
    public decimal MaxIskontoPct { get; set; }

    /// <summary>
    /// Hedef gerçekleşme oranının altında kalındığında
    /// risk gerekçesi oluşturulacak eşiktir.
    ///
    /// Örnek:
    /// 0.80 = %80
    /// </summary>
    public decimal HedefRiskEsigiPct { get; set; }

    /// <summary>
    /// Stok miktarının kaç günlük satışa yetmesi gerektiğini belirler.
    ///
    /// Örnek:
    /// 7 = 7 günlük stok
    /// </summary>
    public decimal StokGunEsigi { get; set; }

    /// <summary>
    /// Teklif değerlendirmesinde kabul edilebilir minimum marjdır.
    ///
    /// Örnek:
    /// 0.00 = %0
    /// </summary>
    public decimal MinMarjRedPct { get; set; }

    /// <summary>
    /// İade oranının hangi seviyeden itibaren risk oluşturacağını belirler.
    ///
    /// Örnek:
    /// 0.10 = %10
    /// </summary>
    public decimal IadeRiskEsigiPct { get; set; }

    /// <summary>
    /// Her risk gerekçesinin toplam risk skoruna katkısını tutar.
    ///
    /// Örnek:
    /// MARJ_ESIK_ALTI -> 40
    /// </summary>
    public Dictionary<string, int> RiskWeights { get; set; }
        = new();

    /// <summary>
    /// Risk skorlarının hangi seviyeye karşılık geldiğini belirler.
    ///
    /// Örnek:
    /// DUSUK_max = 30
    /// ORTA_max = 60
    /// YUKSEK_max = 900
    /// </summary>
    public Dictionary<string, int> RiskLevels { get; set; }
        = new();
}
