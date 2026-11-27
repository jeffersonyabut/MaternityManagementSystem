using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Tests;

public class UniSmsClientTests
{
    [Fact]
    public async Task SendTestSmsAsync_SendsExpectedAuthenticatedRequest()
    {
        const string apiKey = "fake-unisms-key-for-tests";
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://unismsapi.com/api/sms", request.RequestUri?.ToString());
            Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
            Assert.Equal(
                Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey + ":")),
                request.Headers.Authorization?.Parameter);

            return Task.FromResult(JsonResponse(
                HttpStatusCode.Created,
                "pending"));
        });

        await using var harness = await CreateClientAsync(
            StaffRoles.Admin,
            handler,
            apiKey);

        UniSmsSendResult result = await harness.Client.SendTestSmsAsync("09171234567");

        Assert.Contains("pending delivery", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not yet confirmed", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.RequestCount);

        JsonElement body = JsonDocument.Parse(handler.LastRequestBody!).RootElement;
        Assert.Equal("+639171234567", body.GetProperty("recipient").GetString());
        Assert.Equal(UniSmsClient.TestMessage, body.GetProperty("content").GetString());
        Assert.Equal("Unisoft", body.GetProperty("sender_id").GetString());
    }

    [Theory]
    [InlineData("pending", "pending delivery", "not yet confirmed")]
    [InlineData("retrying", "retrying delivery", "not yet confirmed")]
    [InlineData("sent", "reports the test SMS as sent", "not confirmed")]
    [InlineData("failed", "reports that the test SMS failed", "No successful delivery")]
    public async Task SendTestSmsAsync_MapsProviderStatusAccurately(
        string status,
        string expectedText,
        string expectedQualification)
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(
            JsonResponse(HttpStatusCode.Created, status)));
        await using var harness = await CreateClientAsync(StaffRoles.Admin, handler, "fake-key");

        UniSmsSendResult result = await harness.Client.SendTestSmsAsync("+639171234567");

        Assert.Contains(expectedText, result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedQualification, result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendTestSmsAsync_WhenProviderResponseIsMalformed_DoesNotClaimDelivery()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("not-json")
            }));
        await using var harness = await CreateClientAsync(StaffRoles.Admin, handler, "fake-key");

        UniSmsSendResult result = await harness.Client.SendTestSmsAsync("09171234567");

        Assert.Contains("unconfirmed", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("check the provider before sending again", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendTestSmsAsync_WhenUserIsNotAdmin_DoesNotSendRequest()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("The fake handler should not be called."));
        await using var harness = await CreateClientAsync(StaffRoles.Staff, handler, "fake-key");

        UniSmsSendResult result = await harness.Client.SendTestSmsAsync("09171234567");

        Assert.Contains("Administrator access is required", result.UserMessage);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task SendTestSmsAsync_WhenRecipientIsInvalid_DoesNotSendRequest()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("The fake handler should not be called."));
        await using var harness = await CreateClientAsync(StaffRoles.Admin, handler, "fake-key");

        UniSmsSendResult result = await harness.Client.SendTestSmsAsync("5551234");

        Assert.Contains("valid Philippine mobile number", result.UserMessage);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task SendTestSmsAsync_WhenApiKeyIsNotConfigured_DoesNotSendRequest()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("The fake handler should not be called."));
        await using var harness = await CreateClientAsync(StaffRoles.Admin, handler, apiKey: null);

        UniSmsSendResult result = await harness.Client.SendTestSmsAsync("09171234567");

        Assert.Contains("Configure a UniSMS API key", result.UserMessage);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task SendTestSmsAsync_WhenProviderRejectsRequest_ShowsSafeStatusMessage()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("response body must not be shown")
            }));
        await using var harness = await CreateClientAsync(StaffRoles.Admin, handler, "fake-key");

        UniSmsSendResult result = await harness.Client.SendTestSmsAsync("09171234567");

        Assert.Contains("rejected the API key", result.UserMessage);
        Assert.DoesNotContain("response body", result.UserMessage);
        Assert.DoesNotContain("fake-key", result.UserMessage);
    }

    [Fact]
    public async Task SendAppointmentReminderAsync_SendsMessageAndParsesReference()
    {
        const string content = "Your clinic appointment is coming up.";
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://unismsapi.com/api/sms", request.RequestUri?.ToString());
            return Task.FromResult(JsonResponse(
                HttpStatusCode.Created,
                "pending",
                "msg_test-reference_123"));
        });
        await using var harness = await CreateClientAsync(
            StaffRoles.Admin, handler, "fake-key");

        UniSmsApiResult result = await harness.Client.SendAppointmentReminderAsync(
            "09171234567", content);

        Assert.Equal(UniSmsOutcome.Accepted, result.Outcome);
        Assert.Equal("pending", result.ProviderStatus);
        Assert.Equal("msg_test-reference_123", result.ProviderReferenceId);
        Assert.Null(result.FailureCode);
        JsonElement body = JsonDocument.Parse(handler.LastRequestBody!).RootElement;
        Assert.Equal(content, body.GetProperty("content").GetString());
        Assert.Equal("+639171234567", body.GetProperty("recipient").GetString());
    }

    [Fact]
    public async Task SendAppointmentReminderAsync_MapsProviderRejectionSafely()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("clinical content and secret response")
            }));
        await using var harness = await CreateClientAsync(
            StaffRoles.Admin, handler, "fake-key");

        UniSmsApiResult result = await harness.Client.SendAppointmentReminderAsync(
            "09171234567", "private message content");

        Assert.Equal(UniSmsOutcome.Rejected, result.Outcome);
        Assert.Equal("request_rejected", result.FailureCode);
        Assert.DoesNotContain("clinical", result.FailureCode);
        Assert.DoesNotContain("private message", result.FailureCode);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"message\":{\"status\":\"unknown\",\"reference_id\":\"r1\"}}")]
    [InlineData("{\"message\":{\"status\":\"pending\",\"reference_id\":\"../secret\"}}")]
    public async Task SendAppointmentReminderAsync_MalformedResponseIsUnknown(string responseBody)
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(responseBody)
            }));
        await using var harness = await CreateClientAsync(
            StaffRoles.Admin, handler, "fake-key");

        UniSmsApiResult result = await harness.Client.SendAppointmentReminderAsync(
            "09171234567", "reminder");

        Assert.Equal(UniSmsOutcome.Unknown, result.Outcome);
        Assert.Equal("invalid_provider_response", result.FailureCode);
        Assert.Null(result.ProviderReferenceId);
    }

    [Fact]
    public async Task SendAppointmentReminderAsync_NetworkFailureIsUnknown()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            throw new HttpRequestException("secret and message must not escape"));
        await using var harness = await CreateClientAsync(
            StaffRoles.Admin, handler, "fake-key");

        UniSmsApiResult result = await harness.Client.SendAppointmentReminderAsync(
            "09171234567", "reminder content");

        Assert.Equal(UniSmsOutcome.Unknown, result.Outcome);
        Assert.Equal("network_error", result.FailureCode);
    }

    [Fact]
    public async Task GetDeliveryStatusAsync_ParsesStatusAndUsesEscapedReferencePath()
    {
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://unismsapi.com/api/sms/msg_reference-123", request.RequestUri?.ToString());
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, "sent", "msg_reference-123"));
        });
        await using var harness = await CreateClientAsync(
            StaffRoles.Admin, handler, "fake-key");

        UniSmsApiResult result = await harness.Client.GetDeliveryStatusAsync(
            "msg_reference-123");

        Assert.Equal(UniSmsOutcome.Accepted, result.Outcome);
        Assert.Equal("sent", result.ProviderStatus);
        Assert.Equal("msg_reference-123", result.ProviderReferenceId);
    }

    [Fact]
    public async Task GetDeliveryStatusAsync_WhenProviderRejectsLookupReturnsSafeFailure()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("response details")
            }));
        await using var harness = await CreateClientAsync(
            StaffRoles.Admin, handler, "fake-key");

        UniSmsApiResult result = await harness.Client.GetDeliveryStatusAsync(
            "msg_reference-123");

        Assert.Equal(UniSmsOutcome.Rejected, result.Outcome);
        Assert.Equal("reference_not_found", result.FailureCode);
    }

    [Fact]
    public async Task SendAppointmentReminderAsync_InvalidInputDoesNotSend()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("Must not send."));
        await using var harness = await CreateClientAsync(
            StaffRoles.Admin, handler, "fake-key");

        UniSmsApiResult result = await harness.Client.SendAppointmentReminderAsync(
            "invalid", "reminder");

        Assert.Equal(UniSmsOutcome.InvalidRequest, result.Outcome);
        Assert.Equal(0, handler.RequestCount);
    }

    private static async Task<TestHarness> CreateClientAsync(
        string role,
        FakeHttpMessageHandler handler,
        string? apiKey)
    {
        var database = await TestDatabase.CreateAsync();
        var directory = TemporaryDirectory.Create();
        var httpClient = new HttpClient(handler);

        return new TestHarness(
            database,
            directory,
            httpClient,
            new UniSmsClient(
                new FakeCredentialProvider(apiKey, role == StaffRoles.Admin),
                httpClient));
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string messageStatus,
        string? referenceId = null)
    {
        string json = JsonSerializer.Serialize(new
        {
            message = new { status = messageStatus, reference_id = referenceId }
        });

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class FakeHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return await responseFactory(request, cancellationToken);
        }
    }

    private sealed class FakeCredentialProvider(string? apiKey, bool isAdmin)
        : IUniSmsCredentialProvider
    {
        public Task<string?> GetApiKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!isAdmin)
            {
                throw new UnauthorizedAccessException();
            }
            return Task.FromResult(apiKey);
        }

        public Task<string?> GetApiKeyForWorkerAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(apiKey);
        }
    }

    private sealed class TestHarness(
        TestDatabase database,
        TemporaryDirectory directory,
        HttpClient httpClient,
        UniSmsClient client) : IAsyncDisposable
    {
        public UniSmsClient Client { get; } = client;

        public async ValueTask DisposeAsync()
        {
            httpClient.Dispose();
            await database.DisposeAsync();
            directory.Dispose();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; }

        private TemporaryDirectory(string path) => Path = path;

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"GolddieUniSmsClientTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
