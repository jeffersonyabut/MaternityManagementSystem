using System;
using System.IO;
using System.Security;
using System.Text.Json;

namespace MaternityManagementSystem.Themes;

public sealed class ThemePreferenceStore
{
    private readonly string _directoryPath;

    public ThemePreferenceStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Golddie",
            "Preferences"))
    {
    }

    public ThemePreferenceStore(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        _directoryPath = Path.GetFullPath(directoryPath);
    }

    public ThemePreference Load(int staffId)
    {
        string path = GetPreferencePath(staffId);

        try
        {
            if (!File.Exists(path))
            {
                return ThemePreference.System;
            }

            PreferenceDocument? document = JsonSerializer.Deserialize<PreferenceDocument>(
                File.ReadAllText(path));

            return Enum.TryParse(
                    document?.Preference,
                    ignoreCase: false,
                    out ThemePreference preference) &&
                Enum.IsDefined(preference)
                    ? preference
                    : ThemePreference.System;
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            return ThemePreference.System;
        }
    }

    public bool TrySave(int staffId, ThemePreference preference)
    {
        if (!Enum.IsDefined(preference))
        {
            throw new ArgumentOutOfRangeException(nameof(preference));
        }

        string path = GetPreferencePath(staffId);
        string? temporaryPath = null;

        try
        {
            Directory.CreateDirectory(_directoryPath);

            temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
            string json = JsonSerializer.Serialize(
                new PreferenceDocument { Preference = preference.ToString() });
            File.WriteAllText(temporaryPath, json);

            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }

            temporaryPath = null;
            return true;
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            return false;
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception) when (IsStorageException(exception))
                {
                    // A failed cleanup must not affect the active theme.
                }
            }
        }
    }

    private string GetPreferencePath(int staffId)
    {
        if (staffId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(staffId));
        }

        return Path.Combine(_directoryPath, $"theme-{staffId}.json");
    }

    private static bool IsStorageException(Exception exception)
    {
        return exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            NotSupportedException or
            SecurityException;
    }

    private sealed class PreferenceDocument
    {
        public string? Preference { get; set; }
    }
}
