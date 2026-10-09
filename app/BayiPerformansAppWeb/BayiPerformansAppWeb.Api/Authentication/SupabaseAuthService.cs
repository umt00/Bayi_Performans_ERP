using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BayiPerformansAppWeb.Api.Authentication;

internal sealed class SupabaseAuthService(HttpClient httpClient) : ISupabaseAuthService
{
    private readonly HttpClient _httpClient = httpClient;

    public Task<SupabaseAuthResult<SupabaseRegistration>> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "signup")
        {
            Content = JsonContent.Create(new { email, password })
        };

        return SendAsync(
            request,
            SupabaseAuthOperation.Register,
            ParseRegistration,
            cancellationToken);
    }

    public Task<SupabaseAuthResult<SupabaseSession>> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "token?grant_type=password")
        {
            Content = JsonContent.Create(new { email, password })
        };

        return SendAsync(
            request,
            SupabaseAuthOperation.Login,
            ParseSession,
            cancellationToken);
    }

    public Task<SupabaseAuthResult<SupabaseSession>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "token?grant_type=refresh_token")
        {
            Content = JsonContent.Create(new { refresh_token = refreshToken })
        };

        return SendAsync(
            request,
            SupabaseAuthOperation.Refresh,
            ParseSession,
            cancellationToken);
    }

    public Task<SupabaseAuthResult<SupabaseAuthAcknowledgement>> LogoutAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "logout?scope=local");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken);

        return SendAcknowledgementAsync(
            request,
            SupabaseAuthOperation.Logout,
            cancellationToken);
    }

    public Task<SupabaseAuthResult<SupabaseAuthAcknowledgement>> ResendSignupConfirmationAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "resend")
        {
            Content = JsonContent.Create(new { type = "signup", email })
        };

        return SendAcknowledgementAsync(
            request,
            SupabaseAuthOperation.ResendConfirmation,
            cancellationToken);
    }

    private async Task<SupabaseAuthResult<T>> SendAsync<T>(
        HttpRequestMessage request,
        SupabaseAuthOperation operation,
        Func<JsonElement, SupabaseAuthResult<T>> parseSuccess,
        CancellationToken cancellationToken)
    {
        using (request)
        {
            try
            {
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                using var document = await ReadJsonAsync(response.Content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return SupabaseAuthResult<T>.Fail(
                        MapFailure(response, document?.RootElement, operation));
                }

                if (document is null)
                {
                    return UnexpectedResponse<T>();
                }

                return parseSuccess(document.RootElement);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Unavailable<T>();
            }
            catch (HttpRequestException)
            {
                return Unavailable<T>();
            }
            catch (JsonException)
            {
                return UnexpectedResponse<T>();
            }
        }
    }

    private async Task<SupabaseAuthResult<SupabaseAuthAcknowledgement>>
        SendAcknowledgementAsync(
            HttpRequestMessage request,
            SupabaseAuthOperation operation,
            CancellationToken cancellationToken)
    {
        using (request)
        {
            try
            {
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    await response.Content.CopyToAsync(
                        Stream.Null,
                        cancellationToken);
                    return SupabaseAuthResult<SupabaseAuthAcknowledgement>.Success(
                        new SupabaseAuthAcknowledgement());
                }

                using var document = await ReadJsonAsync(
                    response.Content,
                    cancellationToken);
                var root = document?.RootElement;
                var upstreamCode = GetUpstreamErrorCode(root);

                if (operation == SupabaseAuthOperation.Logout
                    && response.StatusCode == HttpStatusCode.Unauthorized
                    && upstreamCode == "session_not_found")
                {
                    return SupabaseAuthResult<SupabaseAuthAcknowledgement>.Success(
                        new SupabaseAuthAcknowledgement());
                }

                return SupabaseAuthResult<SupabaseAuthAcknowledgement>.Fail(
                    MapFailure(response, root, operation));
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return Unavailable<SupabaseAuthAcknowledgement>();
            }
            catch (HttpRequestException)
            {
                return Unavailable<SupabaseAuthAcknowledgement>();
            }
            catch (JsonException)
            {
                return UnexpectedResponse<SupabaseAuthAcknowledgement>();
            }
        }
    }

    private static SupabaseAuthResult<SupabaseRegistration> ParseRegistration(
        JsonElement root)
    {
        if (root.TryGetProperty("access_token", out _))
        {
            var sessionResult = ParseSession(root);
            if (!sessionResult.IsSuccess || sessionResult.Value is null)
            {
                return UnexpectedResponse<SupabaseRegistration>();
            }

            var session = sessionResult.Value;
            return SupabaseAuthResult<SupabaseRegistration>.Success(
                new SupabaseRegistration(
                    session.UserId,
                    session.Email,
                    EmailConfirmationRequired: false,
                    session));
        }

        if (!TryGetUser(root, out var userId, out var email))
        {
            return UnexpectedResponse<SupabaseRegistration>();
        }

        return SupabaseAuthResult<SupabaseRegistration>.Success(
            new SupabaseRegistration(
                userId,
                email,
                EmailConfirmationRequired: true,
                Session: null));
    }

    private static SupabaseAuthResult<SupabaseSession> ParseSession(JsonElement root)
    {
        if (!TryGetRequiredString(root, "access_token", out var accessToken)
            || !TryGetRequiredString(root, "refresh_token", out var refreshToken)
            || !TryGetRequiredString(root, "token_type", out var tokenType)
            || !root.TryGetProperty("expires_in", out var expiresElement)
            || !expiresElement.TryGetInt32(out var expiresIn)
            || expiresIn <= 0
            || !root.TryGetProperty("user", out var user)
            || !TryGetUser(user, out var userId, out var email))
        {
            return UnexpectedResponse<SupabaseSession>();
        }

        return SupabaseAuthResult<SupabaseSession>.Success(
            new SupabaseSession(
                userId,
                email,
                accessToken,
                refreshToken,
                tokenType,
                expiresIn));
    }

    private static bool TryGetUser(
        JsonElement user,
        out Guid userId,
        out string email)
    {
        userId = default;
        email = string.Empty;

        return TryGetRequiredString(user, "id", out var userIdText)
            && Guid.TryParse(userIdText, out userId)
            && TryGetRequiredString(user, "email", out email);
    }

    private static bool TryGetRequiredString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value = property.GetString() ?? string.Empty);
    }

    private static async Task<JsonDocument?> ReadJsonAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);

        try
        {
            return await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SupabaseAuthFailure MapFailure(
        HttpResponseMessage response,
        JsonElement? root,
        SupabaseAuthOperation operation)
    {
        var upstreamCode = GetUpstreamErrorCode(root);
        var upstreamMessage = GetUpstreamMessage(root);
        var retryAfter = response.Headers.RetryAfter?.ToString();

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return new SupabaseAuthFailure(
                StatusCodes.Status429TooManyRequests,
                "COK_FAZLA_ISTEK",
                "Cok fazla kimlik istegi gonderildi. Bir sure sonra tekrar deneyin.",
                retryAfter);
        }

        if (upstreamCode == "invalid_api_key"
            || string.Equals(
                upstreamMessage,
                "Invalid API key",
                StringComparison.OrdinalIgnoreCase))
        {
            return new SupabaseAuthFailure(
                StatusCodes.Status503ServiceUnavailable,
                "KIMLIK_SERVISI_YAPILANDIRILAMADI",
                "Kimlik servisi yapilandirmasi gecersiz.",
                retryAfter);
        }

        if ((int)response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return new SupabaseAuthFailure(
                StatusCodes.Status503ServiceUnavailable,
                "KIMLIK_SERVISI_KULLANILAMIYOR",
                "Kimlik servisi gecici olarak kullanilamiyor.",
                retryAfter);
        }

        if (operation == SupabaseAuthOperation.Refresh
            && response.StatusCode == HttpStatusCode.Conflict)
        {
            return new SupabaseAuthFailure(
                StatusCodes.Status409Conflict,
                "OTURUM_ISLEMI_CAKISTI",
                "Oturum islemi baska bir istekle cakisti. Guncel oturum bilgisiyle tekrar deneyin.",
                retryAfter);
        }

        if (operation == SupabaseAuthOperation.Refresh)
        {
            return new SupabaseAuthFailure(
                StatusCodes.Status401Unauthorized,
                "OTURUM_GECERSIZ",
                "Oturum yenilenemedi. Yeniden giris yapin.",
                retryAfter);
        }

        if (operation == SupabaseAuthOperation.Logout
            && (response.StatusCode is HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden
                || upstreamCode is "bad_jwt" or "session_not_found"))
        {
            return new SupabaseAuthFailure(
                StatusCodes.Status401Unauthorized,
                "OTURUM_GECERSIZ",
                "Oturum dogrulanamadi.",
                retryAfter);
        }

        return upstreamCode switch
        {
            "invalid_credentials" => new SupabaseAuthFailure(
                StatusCodes.Status401Unauthorized,
                "GECERSIZ_KIMLIK_BILGISI",
                "E-posta veya sifre hatali.",
                retryAfter),
            "email_not_confirmed" => new SupabaseAuthFailure(
                StatusCodes.Status401Unauthorized,
                "EPOSTA_DOGRULANMADI",
                "Giris yapmadan once e-posta adresini dogrulayin.",
                retryAfter),
            "user_already_exists" or "email_exists" => new SupabaseAuthFailure(
                StatusCodes.Status400BadRequest,
                "KIMLIK_ISTEGI_REDEDILDI",
                "Kimlik istegi tamamlanamadi.",
                retryAfter),
            "weak_password" => new SupabaseAuthFailure(
                StatusCodes.Status400BadRequest,
                "ZAYIF_SIFRE",
                "Sifre guvenlik kosullarini saglamiyor.",
                retryAfter),
            "signup_disabled" => new SupabaseAuthFailure(
                StatusCodes.Status403Forbidden,
                "KAYIT_KAPALI",
                "Yeni kullanici kaydi su anda kapali.",
                retryAfter),
            _ => new SupabaseAuthFailure(
                StatusCodes.Status400BadRequest,
                "KIMLIK_ISTEGI_REDEDILDI",
                "Kimlik istegi tamamlanamadi.",
                retryAfter)
        };
    }

    private static string GetUpstreamErrorCode(JsonElement? root)
    {
        if (root is not { ValueKind: JsonValueKind.Object } value)
        {
            return string.Empty;
        }

        if (TryGetRequiredString(value, "code", out var code)
            || TryGetRequiredString(value, "error_code", out code))
        {
            return code.Trim().ToLowerInvariant();
        }

        return string.Empty;
    }

    private static string GetUpstreamMessage(JsonElement? root)
    {
        return root is { ValueKind: JsonValueKind.Object } value
            && TryGetRequiredString(value, "message", out var message)
                ? message.Trim()
                : string.Empty;
    }

    private static SupabaseAuthResult<T> Unavailable<T>()
    {
        return SupabaseAuthResult<T>.Fail(
            new SupabaseAuthFailure(
                StatusCodes.Status503ServiceUnavailable,
                "KIMLIK_SERVISI_KULLANILAMIYOR",
                "Kimlik servisi gecici olarak kullanilamiyor.",
                RetryAfter: null));
    }

    private static SupabaseAuthResult<T> UnexpectedResponse<T>()
    {
        return SupabaseAuthResult<T>.Fail(
            new SupabaseAuthFailure(
                StatusCodes.Status503ServiceUnavailable,
                "KIMLIK_SERVISI_GECERSIZ_CEVAP",
                "Kimlik servisinden beklenmeyen bir cevap alindi.",
                RetryAfter: null));
    }

    private enum SupabaseAuthOperation
    {
        Register,
        Login,
        Refresh,
        Logout,
        ResendConfirmation
    }
}
