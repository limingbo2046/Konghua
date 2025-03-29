using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using ParrotMimicry.Services;

namespace ParrotMimicry;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<SettingsPageModel>();//MVVM模式需要注入该类
        builder.Services.AddSingleton<ProjectPageModel>();

        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<DictionaryService>();
        builder.Services.AddSingleton<WhisperService>();
        builder.Services.AddSingleton<SearchSubtitlesService>();
        builder.Services.AddSingleton<SubtitleParserService>();
        builder.Services.AddSingleton<VideoToAudioService>();

        return builder.Build();
    }
}
