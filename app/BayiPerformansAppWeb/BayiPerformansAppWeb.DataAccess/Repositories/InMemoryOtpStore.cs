using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

/// <summary>
/// Thread-safe in-memory OTP depolama implementasyonu.
/// Redis servisinin kapalı olduğu durumlarda veya birim testlerde güvenli fallback olarak çalışır.
/// </summary>
public sealed class InMemoryOtpStore(TimeProvider? timeProvider = null) : IOtpStore
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, MemoryOtpEntry> _otpStore = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _cooldownStore = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, MemoryVerificationTokenEntry> _verificationTokenStore = new(StringComparer.Ordinal);

    private sealed record MemoryOtpEntry(
        OtpRecord Record,
        DateTimeOffset ExpiresAtUtc,
        int CurrentAttempts);

    private sealed record MemoryVerificationTokenEntry(
        OtpVerificationTokenRecord Record,
        DateTimeOffset ExpiresAtUtc);

    public Task SetOtpAsync(
        string islemId,
        OtpRecord record,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(islemId);
        ArgumentNullException.ThrowIfNull(record);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now.Add(ttl);

        _otpStore[islemId] = new MemoryOtpEntry(record, expiresAt, record.MevcutDenemeSayisi);
        return Task.CompletedTask;
    }

    public Task<OtpRecord?> GetOtpAsync(
        string islemId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(islemId);

        if (!_otpStore.TryGetValue(islemId, out var entry))
        {
            return Task.FromResult<OtpRecord?>(null);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (now > entry.ExpiresAtUtc)
        {
            _otpStore.TryRemove(islemId, out _);
            return Task.FromResult<OtpRecord?>(null);
        }

        // Güncel deneme sayısı ile record'u döndür
        var currentRecord = entry.Record with { MevcutDenemeSayisi = entry.CurrentAttempts };
        return Task.FromResult<OtpRecord?>(currentRecord);
    }

    public Task DeleteOtpAsync(
        string islemId,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(islemId))
        {
            _otpStore.TryRemove(islemId, out _);
        }
        return Task.CompletedTask;
    }

    public Task<int> IncrementAttemptCountAsync(
        string islemId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(islemId);

        if (_otpStore.TryGetValue(islemId, out var entry))
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (now > entry.ExpiresAtUtc)
            {
                _otpStore.TryRemove(islemId, out _);
                return Task.FromResult(0);
            }

            int newAttempts = entry.CurrentAttempts + 1;
            _otpStore[islemId] = entry with { CurrentAttempts = newAttempts };
            return Task.FromResult(newAttempts);
        }

        return Task.FromResult(0);
    }

    public Task<bool> TrySetCooldownAsync(
        string hedef,
        string amac,
        TimeSpan cooldownDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hedef);
        ArgumentException.ThrowIfNullOrWhiteSpace(amac);

        string key = $"{hedef.Trim().ToUpperInvariant()}:{amac.Trim().ToUpperInvariant()}";
        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (_cooldownStore.TryGetValue(key, out var existingExpiry) && now < existingExpiry)
        {
            return Task.FromResult(false);
        }

        _cooldownStore[key] = now.Add(cooldownDuration);
        return Task.FromResult(true);
    }

    public Task<TimeSpan?> GetRemainingCooldownAsync(
        string hedef,
        string amac,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hedef);
        ArgumentException.ThrowIfNullOrWhiteSpace(amac);

        string key = $"{hedef.Trim().ToUpperInvariant()}:{amac.Trim().ToUpperInvariant()}";
        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (_cooldownStore.TryGetValue(key, out var existingExpiry))
        {
            if (now < existingExpiry)
            {
                return Task.FromResult<TimeSpan?>(existingExpiry - now);
            }

            _cooldownStore.TryRemove(key, out _);
        }

        return Task.FromResult<TimeSpan?>(null);
    }

    public Task DeleteCooldownAsync(
        string hedef,
        string amac,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(hedef) && !string.IsNullOrWhiteSpace(amac))
        {
            string key = $"{hedef.Trim().ToUpperInvariant()}:{amac.Trim().ToUpperInvariant()}";
            _cooldownStore.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task SetVerificationTokenAsync(
        string tokenHash,
        OtpVerificationTokenRecord record,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentNullException.ThrowIfNull(record);

        _verificationTokenStore[tokenHash] = new MemoryVerificationTokenEntry(
            record,
            _timeProvider.GetUtcNow().Add(ttl));
        return Task.CompletedTask;
    }

    public Task<OtpVerificationTokenRecord?> ConsumeVerificationTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (!_verificationTokenStore.TryRemove(tokenHash, out var entry)
            || _timeProvider.GetUtcNow() > entry.ExpiresAtUtc)
        {
            return Task.FromResult<OtpVerificationTokenRecord?>(null);
        }

        return Task.FromResult<OtpVerificationTokenRecord?>(entry.Record);
    }
}
