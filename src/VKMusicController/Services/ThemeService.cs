using System.Windows.Media;

namespace VKMusicController.Services;

public static class ThemeService
{
    public static void Apply(string theme)
    {
        var resources = System.Windows.Application.Current.Resources;
        var (background, sidebar, surface, input, border, accent) = theme switch
        {
            "Ocean" => ("#151D24", "#19232B", "#202C35", "#17222B", "#34444E", "#9ED8E5"),
            "Ember" => ("#231B1B", "#2A2020", "#312626", "#211A1A", "#493737", "#EFC0A4"),
            _ => ("#171821", "#1C1D28", "#22232F", "#191A24", "#353642", "#C6B6FA")
        };
        Set("UiBackgroundBrush", background);
        Set("UiSidebarBrush", sidebar);
        Set("UiSurfaceBrush", surface);
        Set("UiInputBrush", input);
        Set("UiBorderBrush", border);
        Set("UiAccentBrush", accent);

        void Set(string key, string color) => resources[key] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
    }
}
