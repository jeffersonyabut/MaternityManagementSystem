using MaternityManagementSystem.Themes;

namespace MaternityManagementSystem.Tests;

public sealed class ThemeServiceTests
{
    private sealed class FakeSystemThemeMonitor : ISystemThemeMonitor
    {
        public EffectiveTheme CurrentTheme { get; private set; }

        public event EventHandler? ThemeChanged;

        public FakeSystemThemeMonitor(EffectiveTheme currentTheme)
        {
            CurrentTheme = currentTheme;
        }

        public void ChangeTheme(EffectiveTheme theme)
        {
            CurrentTheme = theme;
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class RecordingThemeResourceApplier : IThemeResourceApplier
    {
        public EffectiveTheme CurrentTheme { get; private set; }

        public int ApplyCount { get; private set; }

        public void Apply(EffectiveTheme theme)
        {
            CurrentTheme = theme;
            ApplyCount++;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"golddie-theme-tests-{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    [Fact]
    public void SetPreference_LightAndDark_ApplyImmediatelyAndPersistForStaff()
    {
        using var directory = new TemporaryDirectory();
        var store = new ThemePreferenceStore(directory.Path);
        var systemTheme = new FakeSystemThemeMonitor(EffectiveTheme.Light);
        var applier = new RecordingThemeResourceApplier();
        using var service = new ThemeService(store, systemTheme, applier);

        service.LoadPreferenceForStaff(14);
        Assert.Equal(EffectiveTheme.Light, applier.CurrentTheme);

        Assert.True(service.SetPreference(ThemePreference.Dark));
        Assert.Equal(ThemePreference.Dark, service.Preference);
        Assert.Equal(EffectiveTheme.Dark, service.EffectiveTheme);
        Assert.Equal(EffectiveTheme.Dark, applier.CurrentTheme);
        Assert.Equal(ThemePreference.Dark, store.Load(14));

        Assert.True(service.SetPreference(ThemePreference.Light));
        Assert.Equal(EffectiveTheme.Light, applier.CurrentTheme);
        Assert.Equal(ThemePreference.Light, store.Load(14));
    }

    [Fact]
    public void SystemPreference_FollowsWindowsThemeChanges()
    {
        using var directory = new TemporaryDirectory();
        var store = new ThemePreferenceStore(directory.Path);
        var systemTheme = new FakeSystemThemeMonitor(EffectiveTheme.Dark);
        var applier = new RecordingThemeResourceApplier();
        using var service = new ThemeService(store, systemTheme, applier);

        service.LoadPreferenceForStaff(22);
        Assert.Equal(ThemePreference.System, service.Preference);
        Assert.Equal(EffectiveTheme.Dark, applier.CurrentTheme);

        int initialApplyCount = applier.ApplyCount;
        systemTheme.ChangeTheme(EffectiveTheme.Light);

        Assert.Equal(EffectiveTheme.Light, service.EffectiveTheme);
        Assert.Equal(EffectiveTheme.Light, applier.CurrentTheme);
        Assert.Equal(initialApplyCount + 1, applier.ApplyCount);
    }

    [Fact]
    public void ChangingPreference_NotifiesThemeConsumers()
    {
        using var directory = new TemporaryDirectory();
        var store = new ThemePreferenceStore(directory.Path);
        var systemTheme = new FakeSystemThemeMonitor(EffectiveTheme.Light);
        var applier = new RecordingThemeResourceApplier();
        using var service = new ThemeService(store, systemTheme, applier);
        ThemeChangedEventArgs? change = null;
        service.ThemeChanged += (_, eventArgs) => change = eventArgs;

        service.LoadPreferenceForStaff(16);
        service.SetPreference(ThemePreference.Dark);

        Assert.NotNull(change);
        Assert.Equal(ThemePreference.Dark, change.Preference);
        Assert.Equal(EffectiveTheme.Dark, change.EffectiveTheme);
    }

    [Fact]
    public void ExplicitPreference_IsNotOverriddenBySystemThemeChanges()
    {
        using var directory = new TemporaryDirectory();
        var store = new ThemePreferenceStore(directory.Path);
        var systemTheme = new FakeSystemThemeMonitor(EffectiveTheme.Light);
        var applier = new RecordingThemeResourceApplier();
        using var service = new ThemeService(store, systemTheme, applier);

        service.LoadPreferenceForStaff(8);
        service.SetPreference(ThemePreference.Dark);
        systemTheme.ChangeTheme(EffectiveTheme.Light);

        Assert.Equal(ThemePreference.Dark, service.Preference);
        Assert.Equal(EffectiveTheme.Dark, service.EffectiveTheme);
        Assert.Equal(EffectiveTheme.Dark, applier.CurrentTheme);
    }

    [Fact]
    public void LoadPreferenceForStaff_RestoresSeparatePreferencesPerStaff()
    {
        using var directory = new TemporaryDirectory();
        var store = new ThemePreferenceStore(directory.Path);
        Assert.True(store.TrySave(3, ThemePreference.Dark));
        Assert.True(store.TrySave(4, ThemePreference.Light));

        var systemTheme = new FakeSystemThemeMonitor(EffectiveTheme.Light);
        var applier = new RecordingThemeResourceApplier();
        using var service = new ThemeService(store, systemTheme, applier);

        service.LoadPreferenceForStaff(3);
        Assert.Equal(ThemePreference.Dark, service.Preference);
        Assert.Equal(EffectiveTheme.Dark, applier.CurrentTheme);

        service.LoadPreferenceForStaff(4);
        Assert.Equal(ThemePreference.Light, service.Preference);
        Assert.Equal(EffectiveTheme.Light, applier.CurrentTheme);
    }

    [Fact]
    public void PreferenceStore_MissingOrMalformedFile_FallsBackToSystem()
    {
        using var directory = new TemporaryDirectory();
        var store = new ThemePreferenceStore(directory.Path);

        Assert.Equal(ThemePreference.System, store.Load(5));

        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(
            System.IO.Path.Combine(directory.Path, "theme-5.json"),
            "{ invalid json");

        Assert.Equal(ThemePreference.System, store.Load(5));
    }

    [Fact]
    public void PreferenceStore_UsesAtomicReplacementWithoutLosingSavedChoice()
    {
        using var directory = new TemporaryDirectory();
        var store = new ThemePreferenceStore(directory.Path);

        Assert.True(store.TrySave(19, ThemePreference.Light));
        Assert.True(store.TrySave(19, ThemePreference.Dark));

        Assert.Equal(ThemePreference.Dark, store.Load(19));
        Assert.Single(Directory.GetFiles(directory.Path, "theme-19.json"));
    }

    [Fact]
    public void PreferenceStore_InvalidStaffIdIsRejected()
    {
        using var directory = new TemporaryDirectory();
        var store = new ThemePreferenceStore(directory.Path);

        Assert.Throws<ArgumentOutOfRangeException>(() => store.Load(0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => store.TrySave(-1, ThemePreference.Dark));
    }

    [Fact]
    public void PreferenceStore_InaccessibleDirectoryDoesNotPreventThemeApplication()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(directory.Path);
        string filePath = System.IO.Path.Combine(directory.Path, "not-a-directory");
        File.WriteAllText(filePath, "occupied");

        var store = new ThemePreferenceStore(filePath);
        var systemTheme = new FakeSystemThemeMonitor(EffectiveTheme.Light);
        var applier = new RecordingThemeResourceApplier();
        using var service = new ThemeService(store, systemTheme, applier);
        service.LoadPreferenceForStaff(12);

        Assert.False(service.SetPreference(ThemePreference.Dark));
        Assert.Equal(EffectiveTheme.Dark, applier.CurrentTheme);
        Assert.Equal("occupied", File.ReadAllText(filePath));
    }
}
