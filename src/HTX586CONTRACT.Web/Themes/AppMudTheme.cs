using MudBlazor;
namespace HTX586CONTRACT.Web.Themes;
public static class AppMudTheme
{
    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#006C5B",
            PrimaryDarken = "#004D40",
            Secondary = "#00A884",
            Success = "#2E7D32",
            Warning = "#ED6C02",
            Error = "#D32F2F",
            Info = "#0288D1",
            Background = "#F4F7F6",
            Surface = "#FFFFFF",
            AppbarBackground = "#006C5B",
            AppbarText = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#253330",
            TextPrimary = "#172B28",
            TextSecondary = "#60706D",
            Divider = "#E2E9E7"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "270px",
            AppbarHeight = "64px"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "Roboto", "Arial", "sans-serif"],
                FontSize = "0.95rem",
                LineHeight = "1.5"
            },
            Button = new ButtonTypography
            {
                TextTransform = "none",
                FontWeight = "600"
            }
        }
    };
}
