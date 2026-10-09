using BayiPerformansAppWeb.DataAccess.Configuration;
using BayiPerformansAppWeb.DataAccess.Repositories;
using BayiPerformansAppWeb.Entities;
using Microsoft.Extensions.Options;

namespace BayiPerformansAppWeb.Api.Tests.DataAccess;

public sealed class RedisOtpStoreTests
{
    [Fact]
    public async Task RedisYokken_ProductionFallbackKapaliysa_IslemBasarisizOlur()
    {
        var store = new RedisOtpStore(
            options: Options.Create(new RedisOptions
            {
                AllowInMemoryFallback = false
            }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SetOtpAsync(
                "islem-1",
                CreateOtpRecord("islem-1"),
                TimeSpan.FromMinutes(3)));

        Assert.Contains("in-memory OTP fallback kapalıdır", exception.Message);
    }

    [Fact]
    public async Task RedisYokken_DevelopmentFallbackAciksa_OtpBellekteSaklanir()
    {
        var store = new RedisOtpStore(
            options: Options.Create(new RedisOptions
            {
                AllowInMemoryFallback = true
            }));

        await store.SetOtpAsync(
            "islem-2",
            CreateOtpRecord("islem-2"),
            TimeSpan.FromMinutes(3));

        OtpRecord? result = await store.GetOtpAsync("islem-2");

        Assert.NotNull(result);
        Assert.Equal("islem-2", result.IslemId);
    }

    private static OtpRecord CreateOtpRecord(string islemId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new OtpRecord(
            islemId,
            "stajyer@example.com",
            OtpAmaclari.Login2Fa,
            "hash",
            "salt",
            3,
            0,
            now,
            now.AddMinutes(3));
    }
}
