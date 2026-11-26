using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MaternityManagementSystem.Services;

public sealed class UniSmsCredentialService
{
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("Golddie.UniSMS.ApiKey.v1");

    private readonly AuthorizationService _authorizationService;
    private readonly string _credentialFilePath;

    public UniSmsCredentialService(
        AuthorizationService authorizationService)
        : this(authorizationService, GetDefaultCredentialFilePath())
    {
    }

    public UniSmsCredentialService(
        AuthorizationService authorizationService,
        string credentialFilePath)
    {
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialFilePath);

        _authorizationService = authorizationService;
        _credentialFilePath = Path.GetFullPath(credentialFilePath);
    }

    public async Task SaveApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAdminAsync();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("An API key is required.", nameof(apiKey));
        }

        cancellationToken.ThrowIfCancellationRequested();

        byte[] plaintextBytes = Encoding.UTF8.GetBytes(apiKey);
        byte[]? protectedBytes = null;
        string? temporaryPath = null;

        try
        {
            protectedBytes = ProtectedData.Protect(
                plaintextBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            string? directory = Path.GetDirectoryName(_credentialFilePath);

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new IOException();
            }

            Directory.CreateDirectory(directory);

            temporaryPath = _credentialFilePath + $".{Guid.NewGuid():N}.tmp";

            await File.WriteAllBytesAsync(
                temporaryPath,
                protectedBytes,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(_credentialFilePath))
            {
                File.Replace(temporaryPath, _credentialFilePath, null);
            }
            else
            {
                File.Move(temporaryPath, _credentialFilePath);
            }

            temporaryPath = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new IOException(
                "The UniSMS API key could not be securely stored.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);

            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }

            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    public async Task<string?> GetApiKeyAsync(
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAdminAsync();

        byte[]? plaintextBytes = await ReadApiKeyBytesAsync(
            cancellationToken);

        if (plaintextBytes is null)
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    public async Task<bool> IsConfiguredAsync(
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAdminAsync();

        byte[]? plaintextBytes = await ReadApiKeyBytesAsync(
            cancellationToken);

        if (plaintextBytes is null)
        {
            return false;
        }

        try
        {
            return plaintextBytes.Length > 0;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    public async Task RemoveApiKeyAsync(
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAdminAsync();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            File.Delete(_credentialFilePath);
        }
        catch (Exception)
        {
            throw new IOException(
                "The UniSMS API key could not be removed from secure storage.");
        }
    }

    private async Task<byte[]?> ReadApiKeyBytesAsync(
        CancellationToken cancellationToken)
    {
        byte[]? protectedBytes = null;

        try
        {
            protectedBytes = await File.ReadAllBytesAsync(
                _credentialFilePath,
                cancellationToken);

            return ProtectedData.Unprotect(
                protectedBytes,
                Entropy,
                DataProtectionScope.CurrentUser);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidOperationException(
                "The UniSMS API key could not be read from secure storage.");
        }
        finally
        {
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
    }

    private static string GetDefaultCredentialFilePath()
    {
        string appDataDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Golddie");

        return Path.Combine(appDataDirectory, "unisms-api-key.dat");
    }

    private static void TryDeleteTemporaryFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Do not expose file-system details from temporary cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Do not expose file-system details from temporary cleanup.
        }
    }
}
