using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaternityManagementSystem.Services;

public interface IUniSmsClient
{
    Task<UniSmsSendResult> SendTestSmsAsync(
        string recipient,
        CancellationToken cancellationToken = default);
}

public sealed record UniSmsSendResult(string UserMessage);

public sealed class UniSmsClient : IUniSmsClient
{
    public const string TestMessage =
        "Golddie test SMS: Your clinic system SMS connection is being tested.";

    private const string ApiBaseUrl = "https://unismsapi.com/api/";
    private const string SenderId = "Unisoft";

    private static readonly HttpClient SharedHttpClient = new();

    private readonly UniSmsCredentialService _credentialService;
    private readonly HttpClient _httpClient;

    public UniSmsClient(
        UniSmsCredentialService credentialService,
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(credentialService);

        _credentialService = credentialService;
        _httpClient = httpClient ?? SharedHttpClient;
    }

    public async Task<UniSmsSendResult> SendTestSmsAsync(
        string recipient,
        CancellationToken cancellationToken = default)
    {
        if (!IsPhilippineMobileNumber(recipient))
        {
            return new UniSmsSendResult(
                "Enter a valid Philippine mobile number (09XXXXXXXXX or +639XXXXXXXXX).");
        }

        string? apiKey;

        try
        {
            apiKey = await _credentialService.GetApiKeyAsync(cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return new UniSmsSendResult(
                "Administrator access is required to send a test SMS.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new UniSmsSendResult(
                "Unable to read the UniSMS API key from secure storage.");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new UniSmsSendResult(
                "Configure a UniSMS API key before sending a test SMS.");
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(new Uri(ApiBaseUrl), "sms"));

            byte[] authorizationBytes = Encoding.UTF8.GetBytes(apiKey + ":");

            try
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(authorizationBytes));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(authorizationBytes);
            }

            request.Content = new StringContent(
                JsonSerializer.Serialize(new SendSmsRequest(
                    NormalizeRecipient(recipient),
                    TestMessage,
                    SenderId)),
                Encoding.UTF8,
                "application/json");

            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new UniSmsSendResult(MapHttpFailure(response.StatusCode));
            }

            string responseJson = await response.Content.ReadAsStringAsync(
                cancellationToken);

            return MapProviderResponse(responseJson);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return UnknownResult();
        }
        catch (OperationCanceledException)
        {
            return UnknownResult();
        }
        catch (IOException)
        {
            return UnknownResult();
        }
        catch (JsonException)
        {
            return new UniSmsSendResult(
                "UniSMS accepted the HTTP request, but its response could not be interpreted. Delivery is unconfirmed; check the provider before sending again.");
        }
        catch (Exception)
        {
            return UnknownResult();
        }
        finally
        {
            apiKey = null;
        }
    }

    private static UniSmsSendResult MapProviderResponse(string responseJson)
    {
        ProviderResponse? providerResponse = JsonSerializer.Deserialize<ProviderResponse>(
            responseJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        string? status = providerResponse?.Message?.Status;

        return status?.Trim().ToLowerInvariant() switch
        {
            "pending" => new UniSmsSendResult(
                "UniSMS accepted the test SMS and it is pending delivery. Delivery is not yet confirmed."),
            "retrying" => new UniSmsSendResult(
                "UniSMS is retrying delivery of the test SMS. Delivery is not yet confirmed."),
            "sent" => new UniSmsSendResult(
                "UniSMS reports the test SMS as sent. Receipt by the recipient is not confirmed."),
            "failed" => new UniSmsSendResult(
                "UniSMS reports that the test SMS failed. No successful delivery was reported."),
            null or "" => new UniSmsSendResult(
                "UniSMS accepted the HTTP request, but returned no message status. Delivery is unconfirmed; check the provider before sending again."),
            _ => new UniSmsSendResult(
                "UniSMS returned an unrecognized message status. Delivery is unconfirmed; check the provider before sending again.")
        };
    }

    private static string MapHttpFailure(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "UniSMS rejected the API key. Verify the saved configuration.",
            HttpStatusCode.UnprocessableEntity =>
                "UniSMS rejected the test SMS request. Check the recipient and sender configuration.",
            HttpStatusCode.TooManyRequests =>
                "UniSMS rate-limited the request. No automatic retry was made.",
            _ =>
                "UniSMS did not accept the test SMS request. Check the provider status before trying again."
        };
    }

    private static UniSmsSendResult UnknownResult()
    {
        return new UniSmsSendResult(
            "The connection ended before Golddie could confirm the UniSMS response. Delivery is unknown; check the provider before sending again.");
    }

    private static bool IsPhilippineMobileNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().Replace(" ", string.Empty)
            .Replace("-", string.Empty);

        return normalized.Length == 11 &&
               normalized.StartsWith("09", StringComparison.Ordinal) &&
               normalized[2..].All(char.IsAsciiDigit) ||
               normalized.Length == 13 &&
               normalized.StartsWith("+639", StringComparison.Ordinal) &&
               normalized[4..].All(char.IsAsciiDigit);
    }

    private static string NormalizeRecipient(string value)
    {
        string normalized = value.Trim().Replace(" ", string.Empty)
            .Replace("-", string.Empty);

        return normalized.StartsWith("09", StringComparison.Ordinal)
            ? "+63" + normalized[1..]
            : normalized;
    }

    private sealed record SendSmsRequest(
        [property: JsonPropertyName("recipient")] string Recipient,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("sender_id")] string SenderId);

    private sealed class ProviderResponse
    {
        [JsonPropertyName("message")]
        public ProviderMessage? Message { get; init; }
    }

    private sealed class ProviderMessage
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }
    }
}
