using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Configuration;
using BayiPerformansAppWeb.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

/// <summary>
/// StackExchange.Redis tabanlı dağıtık OTP ve Rate-Limit depolama implementasyonu.
/// Redis bağlantı hatasında yalnız geliştirme ortamında açıkça izin verilmişse
/// InMemory fallback kullanır. Production'da tek kullanımlılık Redis ile korunur.
/// </summary>
public sealed class RedisOtpStore : IOtpStore
{
    private const string OtpKeyPrefix = "otp:";
    private const string CooldownKeyPrefix = "otp:cooldown:";
    private const string VerificationTokenKeyPrefix = "otp:verification-token:";

    private readonly IConnectionMultiplexer? _redis;
    private readonly InMemoryOtpStore _fallbackStore;
    private readonly ILogger<RedisOtpStore>? _logger;
    private readonly bool _allowInMemoryFallback;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RedisOtpStore(
        IConnectionMultiplexer? redis = null,
        InMemoryOtpStore? fallbackStore = null,
        ILogger<RedisOtpStore>? logger = null,
        TimeProvider? timeProvider = null,
        IOptions<RedisOptions>? options = null)
    {
        _redis = redis;
        _fallbackStore = fallbackStore ?? new InMemoryOtpStore(timeProvider);
        _logger = logger;
        _allowInMemoryFallback = options?.Value.AllowInMemoryFallback ?? true;
    }

    private IDatabase? GetDatabase()
    {
        if (_redis is null || !_redis.IsConnected)
        {
            return null;
        }

        try
        {
            return _redis.GetDatabase();
        }
        catch (Exception ex)
        {
            if (!_allowInMemoryFallback)
            {
                _logger?.LogError(ex, "Redis veritabanı bağlantısı alınamadı.");
                throw;
            }

            _logger?.LogWarning(ex, "Redis veritabanı bağlantısı alınamadı, in-memory fallback kullanılacak.");
            return null;
        }
    }

    private void EnsureFallbackAllowed()
    {
        if (!_allowInMemoryFallback)
        {
            throw new InvalidOperationException(
                "Redis kullanılamıyor ve production ortamında in-memory OTP fallback kapalıdır.");
        }
    }

    public async Task SetOtpAsync(
        string islemId,
        OtpRecord record,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(islemId);
        ArgumentNullException.ThrowIfNull(record);

        var db = GetDatabase();
        if (db is null)
        {
            EnsureFallbackAllowed();
            await _fallbackStore.SetOtpAsync(islemId, record, ttl, cancellationToken);
            return;
        }

        try
        {
            string key = $"{OtpKeyPrefix}{islemId}";
            string json = JsonSerializer.Serialize(record, JsonOptions);
            await db.StringSetAsync(key, json, ttl);
        }
        catch (Exception ex)
        {
            if (!_allowInMemoryFallback)
            {
                _logger?.LogError(ex, "Redis SetOtpAsync hatası.");
                throw;
            }

            _logger?.LogWarning(ex, "Redis SetOtpAsync hatası, fallback store devreye alınıyor.");
            await _fallbackStore.SetOtpAsync(islemId, record, ttl, cancellationToken);
        }
    }

    public async Task<OtpRecord?> GetOtpAsync(
        string islemId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(islemId);

        var db = GetDatabase();
        if (db is null)
        {
            EnsureFallbackAllowed();
            return await _fallbackStore.GetOtpAsync(islemId, cancellationToken);
        }

        try
        {
            string key = $"{OtpKeyPrefix}{islemId}";
            RedisValue value = await db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<OtpRecord>(value.ToString(), JsonOptions);
        }
        catch (Exception ex)
        {
            if (!_allowInMemoryFallback)
            {
                _logger?.LogError(ex, "Redis GetOtpAsync hatası.");
                throw;
            }

            _logger?.LogWarning(ex, "Redis GetOtpAsync hatası, fallback store sorgulanıyor.");
            return await _fallbackStore.GetOtpAsync(islemId, cancellationToken);
        }
    }

    public async Task DeleteOtpAsync(
        string islemId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(islemId))
            return;

        var db = GetDatabase();
        if (db is not null)
        {
            try
            {
                string key = $"{OtpKeyPrefix}{islemId}";
                await db.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                if (!_allowInMemoryFallback)
                {
                    _logger?.LogError(ex, "Redis DeleteOtpAsync hatası.");
                    throw;
                }

                _logger?.LogWarning(ex, "Redis DeleteOtpAsync hatası.");
            }
        }

        await _fallbackStore.DeleteOtpAsync(islemId, cancellationToken);
    }

    public async Task<int> IncrementAttemptCountAsync(
        string islemId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(islemId);

        var record = await GetOtpAsync(islemId, cancellationToken);
        if (record is null)
        {
            return 0;
        }

        int newAttempts = record.MevcutDenemeSayisi + 1;
        var updatedRecord = record with { MevcutDenemeSayisi = newAttempts };

        var db = GetDatabase();
        if (db is not null)
        {
            try
            {
                string key = $"{OtpKeyPrefix}{islemId}";
                TimeSpan? ttl = await db.KeyTimeToLiveAsync(key);
                if (ttl.HasValue && ttl.Value > TimeSpan.Zero)
                {
                    string json = JsonSerializer.Serialize(updatedRecord, JsonOptions);
                    await db.StringSetAsync(key, json, ttl.Value);
                    return newAttempts;
                }
            }
            catch (Exception ex)
            {
                if (!_allowInMemoryFallback)
                {
                    _logger?.LogError(ex, "Redis IncrementAttemptCountAsync hatası.");
                    throw;
                }

                _logger?.LogWarning(ex, "Redis IncrementAttemptCountAsync hatası.");
            }
        }

        EnsureFallbackAllowed();
        return await _fallbackStore.IncrementAttemptCountAsync(islemId, cancellationToken);
    }

    public async Task<bool> TrySetCooldownAsync(
        string hedef,
        string amac,
        TimeSpan cooldownDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hedef);
        ArgumentException.ThrowIfNullOrWhiteSpace(amac);

        var db = GetDatabase();
        if (db is null)
        {
            EnsureFallbackAllowed();
            return await _fallbackStore.TrySetCooldownAsync(hedef, amac, cooldownDuration, cancellationToken);
        }

        try
        {
            string key = $"{CooldownKeyPrefix}{hedef.Trim().ToUpperInvariant()}:{amac.Trim().ToUpperInvariant()}";
            bool setSuccess = await db.StringSetAsync(key, "1", cooldownDuration, When.NotExists);
            return setSuccess;
        }
        catch (Exception ex)
        {
            if (!_allowInMemoryFallback)
            {
                _logger?.LogError(ex, "Redis TrySetCooldownAsync hatası.");
                throw;
            }

            _logger?.LogWarning(ex, "Redis TrySetCooldownAsync hatası, fallback store kullanılıyor.");
            return await _fallbackStore.TrySetCooldownAsync(hedef, amac, cooldownDuration, cancellationToken);
        }
    }

    public async Task<TimeSpan?> GetRemainingCooldownAsync(
        string hedef,
        string amac,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hedef);
        ArgumentException.ThrowIfNullOrWhiteSpace(amac);

        var db = GetDatabase();
        if (db is null)
        {
            EnsureFallbackAllowed();
            return await _fallbackStore.GetRemainingCooldownAsync(hedef, amac, cancellationToken);
        }

        try
        {
            string key = $"{CooldownKeyPrefix}{hedef.Trim().ToUpperInvariant()}:{amac.Trim().ToUpperInvariant()}";
            TimeSpan? ttl = await db.KeyTimeToLiveAsync(key);
            if (ttl.HasValue && ttl.Value > TimeSpan.Zero)
            {
                return ttl.Value;
            }

            return null;
        }
        catch (Exception ex)
        {
            if (!_allowInMemoryFallback)
            {
                _logger?.LogError(ex, "Redis GetRemainingCooldownAsync hatası.");
                throw;
            }

            _logger?.LogWarning(ex, "Redis GetRemainingCooldownAsync hatası.");
            return await _fallbackStore.GetRemainingCooldownAsync(hedef, amac, cancellationToken);
        }
    }

    public async Task DeleteCooldownAsync(
        string hedef,
        string amac,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hedef) || string.IsNullOrWhiteSpace(amac))
        {
            return;
        }

        var db = GetDatabase();
        if (db is not null)
        {
            try
            {
                string key = $"{CooldownKeyPrefix}{hedef.Trim().ToUpperInvariant()}:{amac.Trim().ToUpperInvariant()}";
                await db.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                if (!_allowInMemoryFallback)
                {
                    _logger?.LogError(ex, "Redis DeleteCooldownAsync hatasi.");
                    throw;
                }

                _logger?.LogWarning(ex, "Redis DeleteCooldownAsync hatasi.");
            }
        }

        await _fallbackStore.DeleteCooldownAsync(hedef, amac, cancellationToken);
    }

    public async Task SetVerificationTokenAsync(
        string tokenHash,
        OtpVerificationTokenRecord record,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentNullException.ThrowIfNull(record);

        var db = GetDatabase();
        if (db is null)
        {
            EnsureFallbackAllowed();
            await _fallbackStore.SetVerificationTokenAsync(
                tokenHash,
                record,
                ttl,
                cancellationToken);
            return;
        }

        try
        {
            string key = $"{VerificationTokenKeyPrefix}{tokenHash}";
            string json = JsonSerializer.Serialize(record, JsonOptions);
            await db.StringSetAsync(key, json, ttl);
        }
        catch (Exception ex)
        {
            if (!_allowInMemoryFallback)
            {
                _logger?.LogError(ex, "Redis dogrulama tokeni yazilamadi.");
                throw;
            }

            _logger?.LogWarning(
                ex,
                "Redis dogrulama tokeni yazilamadi, in-memory fallback kullaniliyor.");
            await _fallbackStore.SetVerificationTokenAsync(
                tokenHash,
                record,
                ttl,
                cancellationToken);
        }
    }

    public async Task<OtpVerificationTokenRecord?> ConsumeVerificationTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        var db = GetDatabase();
        if (db is null)
        {
            EnsureFallbackAllowed();
            return await _fallbackStore.ConsumeVerificationTokenAsync(
                tokenHash,
                cancellationToken);
        }

        try
        {
            string key = $"{VerificationTokenKeyPrefix}{tokenHash}";
            RedisValue value = await db.StringGetDeleteAsync(key);
            return value.IsNullOrEmpty
                ? null
                : JsonSerializer.Deserialize<OtpVerificationTokenRecord>(
                    value.ToString(),
                    JsonOptions);
        }
        catch (Exception ex)
        {
            if (!_allowInMemoryFallback)
            {
                _logger?.LogError(ex, "Redis dogrulama tokeni tuketilemedi.");
                throw;
            }

            _logger?.LogWarning(
                ex,
                "Redis dogrulama tokeni tuketilemedi, in-memory fallback kullaniliyor.");
            return await _fallbackStore.ConsumeVerificationTokenAsync(
                tokenHash,
                cancellationToken);
        }
    }
}
