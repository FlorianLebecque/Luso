using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;
using System.Reflection;
using Luso.Features.Home.Pages;
using Luso.Features.Rooms;
using Luso.Features.Rooms.Pages;
using Luso.Features.Rooms.Services;
using Luso.Infrastructure;
using Luso.Shared.Session;

namespace Luso
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .ConfigureMauiHandlers(_ => { });

            // Remove the Android underline and purple accent from all Entry fields.
            // We style the container via Border in XAML instead.
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(
                "FlatEntry",
                (handler, _) =>
                {
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
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddAndroid(android =>
                {
                    android.OnResume(_ => RoomNotifications.SetAppForeground(true));
                    android.OnStop(_ => RoomNotifications.SetAppForeground(false));
                });
            });
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

            var asm = Assembly.GetExecutingAssembly();
            var techCatalog = new RoomTechnologyRegistry();
            techCatalog.ScanAndRegister(asm);
            TargetRegistry.ScanAndRegister(asm);

            // Infrastructure services
            builder.Services.AddSingleton<IRoomTechnologyCatalog>(techCatalog);
            builder.Services.AddSingleton<IRoomSessionStore, RoomSessionStore>();
            builder.Services.AddSingleton<IRoomFactory, RoomFactory>();
            builder.Services.AddTransient<RoomDiscoveryCoordinator>();
            builder.Services.AddTransient<ITaskOrchestrator, TaskOrchestrator>();
            builder.Services.AddTransient<IGuestRosterService, GuestRosterService>();

            // Pages — Shell navigation resolves from DI when types are registered here
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<CreateRoomPage>();
            builder.Services.AddTransient<BrowseRoomsPage>();
            builder.Services.AddTransient<HostRoomPage>();
            builder.Services.AddTransient<GuestRoomPage>();

            return builder.Build();
        }
    }
}