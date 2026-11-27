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

    Task<UniSmsApiResult> SendAppointmentReminderAsync(
        string recipient,
        string content,
        CancellationToken cancellationToken = default,
        Action? requestStarting = null);

    Task<UniSmsApiResult> GetDeliveryStatusAsync(
        string referenceId,
        CancellationToken cancellationToken = default);
}

public sealed record UniSmsSendResult(string UserMessage);

public enum UniSmsOutcome
{
    Accepted,
    Rejected,
    Unknown,
    InvalidRequest,
    CredentialUnavailable
}

/// <summary>Structured and sanitized result from a UniSMS API operation.</summary>
public sealed record UniSmsApiResult(
    UniSmsOutcome Outcome,
    string? ProviderReferenceId = null,
    string? ProviderStatus = null,
    string? FailureCode = null);

public sealed class UniSmsClient : IUniSmsClient
{
    public const string TestMessage =
        "Golddie test SMS: Your clinic system SMS connection is being tested.";

    private const string ApiBaseUrl = "https://unismsapi.com/api/";
    private const string SenderId = "Unisoft";
    private const int MaximumMessageLength = 670;

    private static readonly HttpClient SharedHttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IUniSmsCredentialProvider _credentialService;
    private readonly HttpClient _httpClient;

    public UniSmsClient(
        IUniSmsCredentialProvider credentialService,
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
            UniSmsApiResult result = await SendAsync(
                apiKey,
                recipient,
                TestMessage,
                requireReferenceId: false,
                requestStarting: null,
                cancellationToken);
            return MapTestResult(result);
        }
        finally
        {
            apiKey = null;
        }
    }

    public async Task<UniSmsApiResult> SendAppointmentReminderAsync(
        string recipient,
        string content,
        CancellationToken cancellationToken = default,
        Action? requestStarting = null)
    {
        if (!IsPhilippineMobileNumber(recipient))
        {
            return new UniSmsApiResult(
                UniSmsOutcome.InvalidRequest,
                FailureCode: "invalid_recipient");
        }

        if (string.IsNullOrWhiteSpace(content) || content.Length > MaximumMessageLength)
        {
            return new UniSmsApiResult(
                UniSmsOutcome.InvalidRequest,
                FailureCode: "invalid_content");
        }

        string? apiKey;
        try
        {
            apiKey = await _credentialService.GetApiKeyForWorkerAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new UniSmsApiResult(
                UniSmsOutcome.CredentialUnavailable,
                FailureCode: "credential_unavailable");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new UniSmsApiResult(
                UniSmsOutcome.CredentialUnavailable,
                FailureCode: "credential_not_configured");
        }

        try
        {
            return await SendAsync(
                apiKey,
                recipient,
                content,
                requireReferenceId: true,
                requestStarting,
                cancellationToken);
        }
        finally
        {
            apiKey = null;
        }
    }

    public async Task<UniSmsApiResult> GetDeliveryStatusAsync(
        string referenceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidReferenceId(referenceId))
        {
            return new UniSmsApiResult(
                UniSmsOutcome.InvalidRequest,
                FailureCode: "invalid_reference_id");
        }

        string? apiKey;
        try
        {
            apiKey = await _credentialService.GetApiKeyForWorkerAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new UniSmsApiResult(
                UniSmsOutcome.CredentialUnavailable,
                FailureCode: "credential_unavailable");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new UniSmsApiResult(
                UniSmsOutcome.CredentialUnavailable,
                FailureCode: "credential_not_configured");
        }

        try
        {
            using var request = CreateRequest(
                HttpMethod.Get,
                $"sms/{Uri.EscapeDataString(referenceId)}",
                apiKey);
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return MapHttpFailure(response.StatusCode, isLookup: true);
            }

            return await ParseResponseAsync(
                response, cancellationToken, requireReferenceId: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Unknown("status_lookup_interrupted");
        }
        catch (HttpRequestException)
        {
            return Unknown("network_error");
        }
        catch (IOException)
        {
            return Unknown("response_read_error");
        }
        catch (Exception)
        {
            return Unknown("status_lookup_error");
        }
        finally
        {
            apiKey = null;
        }
    }

    private async Task<UniSmsApiResult> SendAsync(
        string apiKey,
        string recipient,
        string content,
        bool requireReferenceId,
        Action? requestStarting,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, "sms", apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new SendSmsRequest(
                    NormalizeRecipient(recipient), content, SenderId)),
                Encoding.UTF8,
                "application/json");

            cancellationToken.ThrowIfCancellationRequested();
            requestStarting?.Invoke();
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return MapHttpFailure(response.StatusCode, isLookup: false);
            }

            return await ParseResponseAsync(
                response,
                cancellationToken,
                requireReferenceId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Unknown("request_interrupted");
        }
        catch (HttpRequestException)
        {
            return Unknown("network_error");
        }
        catch (IOException)
        {
            return Unknown("response_read_error");
        }
        catch (JsonException)
        {
            return Unknown("invalid_provider_response");
        }
        catch (Exception)
        {
            return Unknown("send_error");
        }
    }

    private static async Task<UniSmsApiResult> ParseResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        bool requireReferenceId)
    {
        try
        {
            string responseJson = await response.Content.ReadAsStringAsync(
                cancellationToken);
            ProviderResponse? providerResponse = JsonSerializer.Deserialize<ProviderResponse>(
                responseJson,
                JsonOptions);
            string? status = NormalizeProviderStatus(
                providerResponse?.Message?.Status);
            string? referenceId = providerResponse?.Message?.ReferenceId;

            if (status is null)
            {
                return Unknown("invalid_provider_response");
            }

            if ((requireReferenceId && string.IsNullOrWhiteSpace(referenceId)) ||
                (referenceId is not null && !IsValidReferenceId(referenceId)))
            {
                return Unknown("invalid_provider_response");
            }

            return new UniSmsApiResult(
                status == "failed" ? UniSmsOutcome.Rejected : UniSmsOutcome.Accepted,
                referenceId,
                status,
                status == "failed" ? "provider_failed" : null);
        }
        catch (JsonException)
        {
            return Unknown("invalid_provider_response");
        }
        catch (IOException)
        {
            return Unknown("response_read_error");
        }
    }

    private static UniSmsSendResult MapTestResult(UniSmsApiResult result) =>
        result.Outcome switch
        {
            UniSmsOutcome.Accepted when result.ProviderStatus == "pending" =>
                new UniSmsSendResult("UniSMS accepted the test SMS and it is pending delivery. Delivery is not yet confirmed."),
            UniSmsOutcome.Accepted when result.ProviderStatus == "retrying" =>
                new UniSmsSendResult("UniSMS is retrying delivery of the test SMS. Delivery is not yet confirmed."),
            UniSmsOutcome.Accepted when result.ProviderStatus == "sent" =>
                new UniSmsSendResult("UniSMS reports the test SMS as sent. Receipt by the recipient is not confirmed."),
            UniSmsOutcome.Rejected when result.ProviderStatus == "failed" =>
                new UniSmsSendResult("UniSMS reports that the test SMS failed. No successful delivery was reported."),
            UniSmsOutcome.Rejected when result.FailureCode == "authentication_rejected" =>
                new UniSmsSendResult("UniSMS rejected the API key. Verify the saved configuration."),
            UniSmsOutcome.Rejected when result.FailureCode == "rate_limited" =>
                new UniSmsSendResult("UniSMS rate-limited the request. No automatic retry was made."),
            UniSmsOutcome.Rejected when result.FailureCode == "request_rejected" =>
                new UniSmsSendResult("UniSMS rejected the test SMS request. Check the recipient and sender configuration."),
            UniSmsOutcome.Rejected =>
                new UniSmsSendResult("UniSMS rejected the test SMS request. Check the recipient and sender configuration."),
            UniSmsOutcome.CredentialUnavailable =>
                new UniSmsSendResult("Unable to read the UniSMS API key from secure storage."),
            UniSmsOutcome.InvalidRequest =>
                new UniSmsSendResult("The test SMS request is invalid."),
            _ when result.FailureCode == "invalid_provider_response" =>
                new UniSmsSendResult(
                    "UniSMS accepted the HTTP request, but its response could not be interpreted. Delivery is unconfirmed; check the provider before sending again."),
            _ => new UniSmsSendResult(
                "The connection ended before Golddie could confirm the UniSMS response. Delivery is unknown; check the provider before sending again.")
        };

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string relativePath,
        string apiKey)
    {
        var request = new HttpRequestMessage(
            method,
            new Uri(new Uri(ApiBaseUrl), relativePath));
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

        return request;
    }

    private static UniSmsApiResult MapHttpFailure(
        HttpStatusCode statusCode,
        bool isLookup)
    {
        if (statusCode == HttpStatusCode.NotFound && isLookup)
        {
            return new UniSmsApiResult(
                UniSmsOutcome.Rejected,
                FailureCode: "reference_not_found");
        }

        if (statusCode is HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden or
            HttpStatusCode.UnprocessableEntity or
            HttpStatusCode.TooManyRequests)
        {
            return new UniSmsApiResult(
                UniSmsOutcome.Rejected,
                FailureCode: statusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "authentication_rejected",
                    HttpStatusCode.UnprocessableEntity => "request_rejected",
                    _ => "rate_limited"
                });
        }

        return Unknown("http_response_unconfirmed");
    }

    private static UniSmsApiResult Unknown(string code) =>
        new(UniSmsOutcome.Unknown, FailureCode: code);

    private static string? NormalizeProviderStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "pending" => "pending",
            "retrying" => "retrying",
            "sent" => "sent",
            "failed" => "failed",
            _ => null
        };

    private static bool IsValidReferenceId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 200 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) ||
                               character is '-' or '_' or '.');

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

        [JsonPropertyName("reference_id")]
        public string? ReferenceId { get; init; }
    }
}
