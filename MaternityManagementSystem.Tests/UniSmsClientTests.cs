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

    private static async Task<TestHarness> CreateClientAsync(
        string role,
        FakeHttpMessageHandler handler,
        string? apiKey)
    {
        var database = await TestDatabase.CreateAsync();
        var directory = TemporaryDirectory.Create();
        int staffId;
        string securityStamp;
        string username = $"unisms-client-test-{Guid.NewGuid():N}";

        await using (var context = await database.Factory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = username,
                NormalizedUsername = UsernameNormalizer.Normalize(username),
                PasswordHash = "unused-test-hash",
                Role = role,
                IsActive = true
            };

            context.StaffMembers.Add(staff);
            await context.SaveChangesAsync();
            staffId = staff.Id;
            securityStamp = staff.SecurityStamp;
        }

        var session = new SessionService();
        session.StartSession(new UserSession(staffId, username, role, securityStamp));
        var authorization = new AuthorizationService(
            session,
            new StaffRepository(database.Factory));
        var credentials = new UniSmsCredentialService(
            authorization,
            Path.Combine(directory.Path, "unisms-api-key.dat"));

        if (apiKey is not null && role == StaffRoles.Admin)
        {
            await credentials.SaveApiKeyAsync(apiKey);
        }

        var httpClient = new HttpClient(handler);

        return new TestHarness(
            database,
            directory,
            httpClient,
            new UniSmsClient(credentials, httpClient));
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string messageStatus)
    {
        string json = JsonSerializer.Serialize(new
        {
            message = new { status = messageStatus }
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
