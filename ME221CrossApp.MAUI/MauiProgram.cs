using ME221CrossApp.MAUI.Services;
using ME221CrossApp.Services;
using Microsoft.Extensions.Logging;

#if ANDROID
using ME221CrossApp.MAUI.Platforms.Android.Services;
#endif

namespace ME221CrossApp.MAUI;

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
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<ConnectionState>();

        builder.Services.AddTransient<ITcpPortCommunicator, TcpDeviceCommunicator>();
#if ANDROID
        builder.Services.AddTransient<ISerialPortCommunicator, AndroidSerialCommunicator>();
#else
        builder.Services.AddTransient<ISerialPortCommunicator, DeviceCommunicator>();
#endif

        builder.Services.AddSingleton<IDeviceCommunicator, HybridDeviceCommunicator>();

#if ANDROID
        builder.Services.AddSingleton<IDeviceDiscoveryService, AndroidDeviceDiscoveryService>();
#else
        builder.Services.AddSingleton<IDeviceDiscoveryService, DesktopDeviceDiscoveryService>();
#endif
        builder.Services.AddSingleton<TcpDeviceDiscoveryService>();

        builder.Services.AddSingleton<IEcuDefinitionService, EcuDefinitionService>();
        builder.Services.AddSingleton<IEcuInteractionService, EcuInteractionService>();

        builder.Services.AddSingleton(FilePicker.Default);

        var app = builder.Build();

        var ecuDefinitionService = app.Services.GetRequiredService<IEcuDefinitionService>();
        ecuDefinitionService.LoadFromStoreAsync();
        return app;
    }
}