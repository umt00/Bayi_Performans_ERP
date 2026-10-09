using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities;
using BayiPerformansAppWeb.Entities.ReadModels;

namespace BayiPerformansAppWeb.Business.Services;

/// <summary>
/// Çoklu ürün içeren tekliflerin oluşturulması, saklanması ve deterministik değerlendirilmesini sağlayan servis.
/// </summary>
public sealed class TeklifYonetimService : ITeklifYonetimService
{
    private readonly ITeklifWriteRepository _teklifRepository;
    private readonly IBayiReadRepository _bayiReadRepository;
    private readonly IUrunReadRepository _urunReadRepository;
    private readonly IDepoReadRepository _depoReadRepository;
    private readonly IStokReadRepository _stokReadRepository;
    private readonly IBayiDonemPerformansReadRepository? _bayiDonemPerformansRepository;
    private readonly OfferDecisionEngine _decisionEngine;
    private readonly BusinessRulesOptions _options;
    private readonly TimeProvider _timeProvider;

    public TeklifYonetimService(
        ITeklifWriteRepository teklifRepository,
        IBayiReadRepository bayiReadRepository,
        IUrunReadRepository urunReadRepository,
        IDepoReadRepository depoReadRepository,
        IStokReadRepository stokReadRepository,
        OfferDecisionEngine decisionEngine,
        BusinessRulesOptions options,
        TimeProvider? timeProvider = null)
        : this(
            teklifRepository,
            bayiReadRepository,
            urunReadRepository,
            depoReadRepository,
            stokReadRepository,
            decisionEngine,
            options,
            bayiDonemPerformansRepository: null,
            timeProvider: timeProvider)
    {
    }

    public TeklifYonetimService(
        ITeklifWriteRepository teklifRepository,
        IBayiReadRepository bayiReadRepository,
        IUrunReadRepository urunReadRepository,
        IDepoReadRepository depoReadRepository,
        IStokReadRepository stokReadRepository,
        OfferDecisionEngine decisionEngine,
        BusinessRulesOptions options,
        IBayiDonemPerformansReadRepository? bayiDonemPerformansRepository,
        TimeProvider? timeProvider = null)
    {
        _teklifRepository = teklifRepository ?? throw new ArgumentNullException(nameof(teklifRepository));
        _bayiReadRepository = bayiReadRepository ?? throw new ArgumentNullException(nameof(bayiReadRepository));
        _urunReadRepository = urunReadRepository ?? throw new ArgumentNullException(nameof(urunReadRepository));
        _depoReadRepository = depoReadRepository ?? throw new ArgumentNullException(nameof(depoReadRepository));
        _stokReadRepository = stokReadRepository ?? throw new ArgumentNullException(nameof(stokReadRepository));
        _decisionEngine = decisionEngine ?? throw new ArgumentNullException(nameof(decisionEngine));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _bayiDonemPerformansRepository = bayiDonemPerformansRepository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ServiceResult<TeklifModel>> TeklifOlusturAsync(
        YeniTeklifCommand command,
        CancellationToken ct = default)
    {
        var validationResult = await DogrulaCommandAsync(command, ct);
        if (!validationResult.IsSuccess)
        {
            return ServiceResult<TeklifModel>.Fail(
                validationResult.ErrorKind,
                validationResult.ErrorCode!,
                validationResult.ErrorDetail!);
        }

        var bayi = validationResult.Value!;
        string teklifId = $"TKLF-{_timeProvider.GetUtcNow().Year}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        var kalemler = new List<TeklifKalemModel>();
        decimal toplamNetTutar = 0;
        int siraNo = 1;

        foreach (var k in command.Kalemler)
        {
            var urun = await _urunReadRepository.GetByCodeAsync(k.UrunKodu, ct);
            var depo = await _depoReadRepository.GetByCodeAsync(k.DepoKodu, ct);

            decimal netTutar = k.Miktar * k.BirimFiyat * (1m - k.IskontoOrani);
            toplamNetTutar += netTutar;

            kalemler.Add(new TeklifKalemModel(
                SiraNo: siraNo++,
                UrunKodu: k.UrunKodu,
                UrunAdi: urun?.UrunAdi ?? k.UrunKodu,
                DepoKodu: k.DepoKodu,
                DepoAdi: depo?.DepoAdi ?? k.DepoKodu,
                Miktar: k.Miktar,
                BirimFiyat: k.BirimFiyat,
                IskontoOrani: k.IskontoOrani,
                NetTutar: Math.Round(netTutar, 2, MidpointRounding.AwayFromZero)));
        }

        var teklif = new TeklifModel(
            TeklifId: teklifId,
            BayiKodu: command.BayiKodu,
            BayiAdi: bayi.BayiAdi,
            Aciklama: command.Aciklama,
            Durum: TeklifDurumlari.Taslak,
            ToplamNetTutar: Math.Round(toplamNetTutar, 2, MidpointRounding.AwayFromZero),
            OlusturulmaTarihiUtc: _timeProvider.GetUtcNow(),
            Kalemler: kalemler,
            BayiSehir: bayi.Sehir,
            BayiBolge: bayi.Bolge);

        await _teklifRepository.TeklifKaydetAsync(teklif, ct);
        return ServiceResult<TeklifModel>.Success(teklif);
    }

    public async Task<ServiceResult<TeklifCokluDegerlendirmeOutput>> TeklifDegerlendirByIdAsync(
        string teklifId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(teklifId))
        {
            return ServiceResult<TeklifCokluDegerlendirmeOutput>.Fail(
                ServiceErrorKind.Validation,
                "GECERSIZ_TEKLIF_ID",
                "Teklif ID belirtilmelidir.");
        }

        var teklif = await _teklifRepository.TeklifGetirAsync(teklifId, ct);
        if (teklif is null)
        {
            return ServiceResult<TeklifCokluDegerlendirmeOutput>.Fail(
                ServiceErrorKind.NotFound,
                "TEKLIF_BULUNAMADI",
                $"'{teklifId}' ID'li teklif bulunamadı.");
        }

        var degerlendirmeResult = await DegerlendirKalemlerAsync(
            teklif.TeklifId,
            teklif.BayiKodu,
            teklif.Kalemler.Select(k => new TeklifKalemCommand(
                k.UrunKodu, k.DepoKodu, k.Miktar, k.BirimFiyat, k.IskontoOrani)).ToList(),
            ct);

        if (!degerlendirmeResult.IsSuccess || degerlendirmeResult.Value is null)
        {
            return degerlendirmeResult;
        }

        var guncelTeklif = teklif with
        {
            Durum = TeklifDurumlari.Degerlendirildi,
            DegerlendirmeSonucu = degerlendirmeResult.Value
        };

        await _teklifRepository.TeklifGuncelleAsync(guncelTeklif, ct);

        var updatedTeklif = await _teklifRepository.TeklifGetirAsync(teklifId, ct);
        if (updatedTeklif?.AiYorum is not null)
        {
            return ServiceResult<TeklifCokluDegerlendirmeOutput>.Success(
                degerlendirmeResult.Value with
                {
                    AiComment = updatedTeklif.AiYorum.AiComment,
                    AlertMessage = updatedTeklif.AiYorum.AlertMessage,
                    IsFallback = updatedTeklif.AiYorum.IsFallback
                });
        }

        return degerlendirmeResult;
    }

    public async Task<ServiceResult<TeklifCokluDegerlendirmeOutput>> TeklifAnlikDegerlendirAsync(
        YeniTeklifCommand command,
        CancellationToken ct = default)
    {
        var validationResult = await DogrulaCommandAsync(command, ct);
        if (!validationResult.IsSuccess)
        {
            return ServiceResult<TeklifCokluDegerlendirmeOutput>.Fail(
                validationResult.ErrorKind,
                validationResult.ErrorCode!,
                validationResult.ErrorDetail!);
        }

        string tempTeklifId = $"ANLIK-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        return await DegerlendirKalemlerAsync(tempTeklifId, command.BayiKodu, command.Kalemler, ct);
    }

    public async Task<ServiceResult<TeklifModel>> TeklifGetirAsync(
        string teklifId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(teklifId))
        {
            return ServiceResult<TeklifModel>.Fail(
                ServiceErrorKind.Validation,
                "GECERSIZ_TEKLIF_ID",
                "Teklif ID belirtilmelidir.");
        }

        var teklif = await _teklifRepository.TeklifGetirAsync(teklifId, ct);
        if (teklif is null)
        {
            return ServiceResult<TeklifModel>.Fail(
                ServiceErrorKind.NotFound,
                "TEKLIF_BULUNAMADI",
                $"'{teklifId}' ID'li teklif bulunamadı.");
        }

        return ServiceResult<TeklifModel>.Success(teklif);
    }

    public async Task<ServiceResult<IReadOnlyList<TeklifModel>>> TeklifleriListeleAsync(
        TeklifListeFiltre filtre,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtre);
        var list = await _teklifRepository.TeklifleriListeleAsync(filtre, ct);
        return ServiceResult<IReadOnlyList<TeklifModel>>.Success(list);
    }

    private async Task<ServiceResult<BayiReadModel>> DogrulaCommandAsync(
        YeniTeklifCommand command,
        CancellationToken ct)
    {
        if (command is null)
        {
            return ServiceResult<BayiReadModel>.Fail(
                ServiceErrorKind.Validation, "GECERSIZ_KOMUT", "Teklif gövdesi boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(command.BayiKodu))
        {
            return ServiceResult<BayiReadModel>.Fail(
                ServiceErrorKind.Validation, "GECERSIZ_BAYI_KODU", "Bayi kodu belirtilmelidir.");
        }

        if (command.Kalemler is null || command.Kalemler.Count == 0)
        {
            return ServiceResult<BayiReadModel>.Fail(
                ServiceErrorKind.Validation, "GECERSIZ_KALEMLER", "Teklif en az bir ürün kalemi içermelidir.");
        }

        var bayi = await _bayiReadRepository.GetByCodeAsync(command.BayiKodu, ct);
        if (bayi is null)
        {
            return ServiceResult<BayiReadModel>.Fail(
                ServiceErrorKind.NotFound, "BAYI_BULUNAMADI", $"'{command.BayiKodu}' kodlu bayi bulunamadı.");
        }

        for (int i = 0; i < command.Kalemler.Count; i++)
        {
            var k = command.Kalemler[i];
            if (string.IsNullOrWhiteSpace(k.UrunKodu))
            {
                return ServiceResult<BayiReadModel>.Fail(
                    ServiceErrorKind.Validation, "GECERSIZ_URUN_KODU", $"{i + 1}. kalemde ürün kodu zorunludur.");
            }
            if (string.IsNullOrWhiteSpace(k.DepoKodu))
            {
                return ServiceResult<BayiReadModel>.Fail(
                    ServiceErrorKind.Validation, "GECERSIZ_DEPO_KODU", $"{i + 1}. kalemde depo kodu zorunludur.");
            }
            if (k.Miktar <= 0)
            {
                return ServiceResult<BayiReadModel>.Fail(
                    ServiceErrorKind.Validation, "GECERSIZ_MIKTAR", $"{i + 1}. kalemde miktar 0'dan büyük olmalıdır.");
            }
            if (k.BirimFiyat <= 0)
            {
                return ServiceResult<BayiReadModel>.Fail(
                    ServiceErrorKind.Validation, "GECERSIZ_FIYAT", $"{i + 1}. kalemde birim fiyat 0'dan büyük olmalıdır.");
            }
            if (k.IskontoOrani < 0 || k.IskontoOrani > 1)
            {
                return ServiceResult<BayiReadModel>.Fail(
                    ServiceErrorKind.Validation, "GECERSIZ_ISKONTO", $"{i + 1}. kalemde iskonto oranı 0 ile 1 arasında olmalıdır.");
            }

            var urun = await _urunReadRepository.GetByCodeAsync(k.UrunKodu, ct);
            if (urun is null)
            {
                return ServiceResult<BayiReadModel>.Fail(
                    ServiceErrorKind.NotFound, "URUN_BULUNAMADI", $"'{k.UrunKodu}' kodlu ürün bulunamadı.");
            }

            var depo = await _depoReadRepository.GetByCodeAsync(k.DepoKodu, ct);
            if (depo is null)
            {
                return ServiceResult<BayiReadModel>.Fail(
                    ServiceErrorKind.NotFound, "DEPO_BULUNAMADI", $"'{k.DepoKodu}' kodlu depo bulunamadı.");
            }
        }

        return ServiceResult<BayiReadModel>.Success(bayi);
    }

    private async Task<ServiceResult<TeklifCokluDegerlendirmeOutput>> DegerlendirKalemlerAsync(
        string teklifId,
        string bayiKodu,
        IReadOnlyList<TeklifKalemCommand> kalemler,
        CancellationToken ct)
    {
        var kalemSonuclari = new List<TeklifKalemDegerlendirmeOutput>();
        var genelGerekceler = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        decimal toplamNetTutar = 0;
        decimal toplamMaliyet = 0;
        bool hasReject = false;
        bool hasNeedApproval = false;
        int siraNo = 1;

        foreach (var k in kalemler)
        {
            var urun = await _urunReadRepository.GetByCodeAsync(k.UrunKodu, ct);
            var stok = await _stokReadRepository.GetAsync(k.UrunKodu, k.DepoKodu, ct);

            decimal birimMaliyet = urun?.BirimMaliyet ?? 0m;
            decimal mevcutStok = stok?.MevcutStok ?? 0m;
            bool stokYeterli = mevcutStok >= k.Miktar;

            decimal netBirimFiyat = k.BirimFiyat * (1m - k.IskontoOrani);
            decimal marjPct = netBirimFiyat > 0 ? (netBirimFiyat - birimMaliyet) / netBirimFiyat : 0m;
            decimal netTutar = k.Miktar * netBirimFiyat;
            decimal toplamKalemMaliyet = k.Miktar * birimMaliyet;

            toplamNetTutar += netTutar;
            toplamMaliyet += toplamKalemMaliyet;

            var decision = _decisionEngine.Evaluate(
                miktar: k.Miktar,
                birimFiyat: k.BirimFiyat,
                iskontoOrani: k.IskontoOrani,
                birimMaliyet: birimMaliyet,
                mevcutStok: stok?.MevcutStok);

            if (string.Equals(decision.Karar, "REJECT", StringComparison.OrdinalIgnoreCase))
            {
                hasReject = true;
            }
            else if (string.Equals(decision.Karar, "NEED_APPROVAL", StringComparison.OrdinalIgnoreCase))
            {
                hasNeedApproval = true;
            }

            foreach (var code in decision.GerekceKodlari)
            {
                genelGerekceler.Add(code);
            }

            kalemSonuclari.Add(new TeklifKalemDegerlendirmeOutput(
                SiraNo: siraNo++,
                UrunKodu: k.UrunKodu,
                UrunAdi: urun?.UrunAdi ?? k.UrunKodu,
                DepoKodu: k.DepoKodu,
                Miktar: k.Miktar,
                BirimFiyat: k.BirimFiyat,
                IskontoOrani: k.IskontoOrani,
                BirimMaliyet: birimMaliyet,
                NetTutar: Math.Round(netTutar, 2, MidpointRounding.AwayFromZero),
                MarjPct: Math.Round(marjPct, 4, MidpointRounding.AwayFromZero),
                MevcutStok: mevcutStok,
                StokYeterliMi: stokYeterli,
                Karar: decision.Karar,
                GerekceKodlari: decision.GerekceKodlari.ToList()));
        }

        string genelKarar = hasReject ? "REJECT" : (hasNeedApproval ? "NEED_APPROVAL" : "APPROVE");
        decimal genelMarjPct = toplamNetTutar > 0 ? (toplamNetTutar - toplamMaliyet) / toplamNetTutar : 0m;

        BayiDonemPerformansReadModel? dealerPerf = null;
        if (_bayiDonemPerformansRepository is not null)
        {
            dealerPerf = await _bayiDonemPerformansRepository.GetAsync(bayiKodu, donem: null, ct);
        }

        if (dealerPerf?.GerekceKodlari is not null)
        {
            foreach (var code in dealerPerf.GerekceKodlari)
            {
                if (!string.IsNullOrWhiteSpace(code))
                {
                    genelGerekceler.Add(code);
                }
            }
        }

        var output = new TeklifCokluDegerlendirmeOutput(
            TeklifId: teklifId,
            BayiKodu: bayiKodu,
            GenelKarar: genelKarar,
            GenelGerekceKodlari: genelGerekceler.ToList(),
            ToplamNetTutar: Math.Round(toplamNetTutar, 2, MidpointRounding.AwayFromZero),
            ToplamMaliyet: Math.Round(toplamMaliyet, 2, MidpointRounding.AwayFromZero),
            GenelMarjPct: Math.Round(genelMarjPct, 4, MidpointRounding.AwayFromZero),
            DegerlendirilmeTarihiUtc: _timeProvider.GetUtcNow(),
            KalemSonuclari: kalemSonuclari,
            RiskSkoru: dealerPerf?.RiskSkoru ?? 0,
            RiskSeviyesi: dealerPerf?.RiskSeviyesi ?? "DUSUK",
            HedefTutturmaYuzdesi: dealerPerf?.HedefTutmaOrani ?? 0m,
            OrtalamaIskontoOrani: dealerPerf?.OrtalamaIskontoOrani ?? 0m,
            IadeOrani: dealerPerf?.OrtalamaIadeOrani ?? 0m);

        return ServiceResult<TeklifCokluDegerlendirmeOutput>.Success(output);
    }
}
