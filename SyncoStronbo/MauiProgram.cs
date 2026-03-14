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
                })
                .ConfigureMauiHandlers(_ => { });

        // Remove the Android underline and purple accent from all Entry fields.
        // We style the container via Border in XAML instead.
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(
            "FlatEntry",
            (handler, _) => {
#if ANDROID
                // Transparent underline
                handler.PlatformView.BackgroundTintList =
                    Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                // White caret
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
                {
                    var cursor = Android.Graphics.PorterDuff.Mode.SrcIn!;
                    handler.PlatformView.TextCursorDrawable?.SetColorFilter(
                        new Android.Graphics.PorterDuffColorFilter(
                            Android.Graphics.Color.White, cursor));
                }
#endif
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