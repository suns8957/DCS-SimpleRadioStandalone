using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

namespace Ciribob.DCS.SimpleRadio.Standalone.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            //- Our documentation site: https://docs.microsoft.com/dotnet/communitytoolkit/maui
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("RobotoMono-VariableFont.ttf", "RobotoMono");
            });

        // Add NLog for Logging
        builder.Logging.ClearProviders();
        builder.Logging.AddNLog();

#if DEBUG
        var logger = LogManager.Setup().RegisterMauiLog()
            .LoadConfiguration(c => c.ForLogger(NLog.LogLevel.Debug).WriteToConsole())
            .GetCurrentClassLogger();
#else
        var logger = NLog.LogManager.Setup().RegisterMauiLog()
            .LoadConfiguration(c => c.ForLogger(NLog.LogLevel.Info).WriteToMauiLog())
            .GetCurrentClassLogger();
#endif

        return builder.Build();
    }
}