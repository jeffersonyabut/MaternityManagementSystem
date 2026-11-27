namespace MaternityManagementSystem.Themes;

public interface ISystemThemeMonitor
{
    EffectiveTheme CurrentTheme { get; }

    event EventHandler? ThemeChanged;
}

public interface IThemeResourceApplier
{
    void Apply(EffectiveTheme theme);
}

public sealed class ThemeChangedEventArgs : EventArgs
{
    public ThemePreference Preference { get; }

    public EffectiveTheme EffectiveTheme { get; }

    public ThemeChangedEventArgs(
        ThemePreference preference,
        EffectiveTheme effectiveTheme)
    {
        Preference = preference;
        EffectiveTheme = effectiveTheme;
    }
}

public sealed class ThemeService : IDisposable
{
    private readonly ThemePreferenceStore _preferenceStore;
    private readonly ISystemThemeMonitor _systemThemeMonitor;
    private readonly IThemeResourceApplier _resourceApplier;
    private int? _staffId;
    private bool _isDisposed;

    public ThemePreference Preference { get; private set; } = ThemePreference.System;

    public EffectiveTheme EffectiveTheme { get; private set; } = EffectiveTheme.Light;

    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public ThemeService(
        ThemePreferenceStore preferenceStore,
        ISystemThemeMonitor systemThemeMonitor,
        IThemeResourceApplier resourceApplier)
    {
        ArgumentNullException.ThrowIfNull(preferenceStore);
        ArgumentNullException.ThrowIfNull(systemThemeMonitor);
        ArgumentNullException.ThrowIfNull(resourceApplier);

        _preferenceStore = preferenceStore;
        _systemThemeMonitor = systemThemeMonitor;
        _resourceApplier = resourceApplier;

        _systemThemeMonitor.ThemeChanged += SystemThemeMonitor_ThemeChanged;
        ApplyEffectiveTheme(_systemThemeMonitor.CurrentTheme);
    }

    public void LoadPreferenceForStaff(int staffId)
    {
        if (staffId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(staffId));
        }

        _staffId = staffId;
        Preference = _preferenceStore.Load(staffId);
        ApplyPreference();
    }

    public bool SetPreference(ThemePreference preference)
    {
        if (!Enum.IsDefined(preference))
        {
            throw new ArgumentOutOfRangeException(nameof(preference));
        }

        Preference = preference;
        ApplyPreference();

        return _staffId.HasValue &&
            _preferenceStore.TrySave(_staffId.Value, preference);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _systemThemeMonitor.ThemeChanged -= SystemThemeMonitor_ThemeChanged;

        if (_systemThemeMonitor is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _isDisposed = true;
    }

    private void ApplyPreference()
    {
        EffectiveTheme theme = Preference switch
        {
            ThemePreference.Light => EffectiveTheme.Light,
            ThemePreference.Dark => EffectiveTheme.Dark,
            _ => _systemThemeMonitor.CurrentTheme
        };

        ApplyEffectiveTheme(theme);
    }

    private void ApplyEffectiveTheme(EffectiveTheme theme)
    {
        EffectiveTheme = theme;
        _resourceApplier.Apply(theme);
        ThemeChanged?.Invoke(
            this,
            new ThemeChangedEventArgs(Preference, EffectiveTheme));
    }

    private void SystemThemeMonitor_ThemeChanged(
        object? sender,
        EventArgs e)
    {
        if (Preference == ThemePreference.System)
        {
            ApplyPreference();
        }
    }
}
