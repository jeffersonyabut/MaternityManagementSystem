using System.Windows;

namespace MaternityManagementSystem.Themes;

public sealed class WpfThemeResourceApplier : IThemeResourceApplier
{
    private const string PaletteMarkerKey = "GolddieThemePaletteMarker";
    private readonly ResourceDictionary _applicationResources;

    public WpfThemeResourceApplier(ResourceDictionary applicationResources)
    {
        ArgumentNullException.ThrowIfNull(applicationResources);
        _applicationResources = applicationResources;
    }

    public void Apply(EffectiveTheme theme)
    {
        string paletteName = theme switch
        {
            EffectiveTheme.Light => "LightTheme.xaml",
            EffectiveTheme.Dark => "DarkTheme.xaml",
            _ => throw new ArgumentOutOfRangeException(nameof(theme))
        };

        var palette = new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/MaternityManagementSystem;component/Themes/{paletteName}",
                UriKind.Absolute)
        };

        int currentPaletteIndex = -1;
        for (int index = 0;
             index < _applicationResources.MergedDictionaries.Count;
             index++)
        {
            if (_applicationResources.MergedDictionaries[index]
                .Contains(PaletteMarkerKey))
            {
                currentPaletteIndex = index;
                break;
            }
        }

        if (currentPaletteIndex >= 0)
        {
            _applicationResources.MergedDictionaries[currentPaletteIndex] = palette;
        }
        else
        {
            _applicationResources.MergedDictionaries.Add(palette);
        }
    }
}
