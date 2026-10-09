using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities;

namespace BayiPerformansAppWeb.Business.Abstractions;

/// <summary>
/// Zamanlanmış (scheduled) bayi metrik ve risk skoru arka plan hesaplama servisi sözleşmesi.
/// </summary>
public interface IScheduledScoreJobService
{
    /// <summary>
    /// Tüm bayilerin performans ve risk metriklerini toplu hesaplar ve önbelleğe/depoya yazar.
    /// </summary>
    Task<ServiceResult<JobDurumBilgisi>> SkorlariHesaplaVeOnbellekleAsync(CancellationToken ct = default);

    /// <summary>
    /// Arka plan hesaplama görevinin son çalışma zamanını ve durumunu getirir.
    /// </summary>
    Task<ServiceResult<JobDurumBilgisi>> GetirJobDurumuAsync(CancellationToken ct = default);
}
