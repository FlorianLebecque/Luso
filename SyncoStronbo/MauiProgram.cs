using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;
using SyncoStronbo.Features.Rooms.Services;

namespace SyncoStronbo {
    public static class MauiProgram {
        public static MauiApp CreateMauiApp() {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts => {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if ANDROID
            builder.ConfigureLifecycleEvents(events => {
                events.AddAndroid(android => {
                    android.OnResume(_ => RoomNotifications.SetAppForeground(true));
                    android.OnStop(_ => RoomNotifications.SetAppForeground(false));
                });
            });
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}