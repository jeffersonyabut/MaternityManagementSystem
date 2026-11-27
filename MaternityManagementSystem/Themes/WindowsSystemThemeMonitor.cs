using Microsoft.Win32;
using System.IO;
using System.Windows.Threading;

namespace MaternityManagementSystem.Themes;

public sealed class WindowsSystemThemeMonitor : ISystemThemeMonitor, IDisposable
{
    private const string PersonalizeRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly Dispatcher _dispatcher;
    private bool _isDisposed;

    public EffectiveTheme CurrentTheme => ReadCurrentTheme();

    public event EventHandler? ThemeChanged;

    public WindowsSystemThemeMonitor(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        _isDisposed = true;
    }

    private void SystemEvents_UserPreferenceChanged(
        object sender,
        UserPreferenceChangedEventArgs e)
    {
        if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            return;
        }

        _dispatcher.BeginInvoke(new Action(() =>
            ThemeChanged?.Invoke(this, EventArgs.Empty)));
    }

    private static EffectiveTheme ReadCurrentTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                PersonalizeRegistryPath,
                writable: false);

            object? value = key?.GetValue("AppsUseLightTheme") ??
                key?.GetValue("SystemUsesLightTheme");

            return value is int useLightTheme && useLightTheme == 0
                ? EffectiveTheme.Dark
                : EffectiveTheme.Light;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                System.Security.SecurityException)
        {
            return EffectiveTheme.Light;
        }
    }
}
