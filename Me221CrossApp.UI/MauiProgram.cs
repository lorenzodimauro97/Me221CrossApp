using ME221CrossApp.Services;
using Me221CrossApp.UI.Services;
using ME221CrossApp.UI.Services;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
#if ANDROID
using Me221CrossApp.UI.Services;
#elif WINDOWS
using Me221CrossApp.UI.Services.Windows;
#endif


namespace ME221CrossApp.UI
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
                });

            var logPath = Path.Combine(FileSystem.AppDataDirectory, "logs", "app-.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .WriteTo.Console()
                .CreateLogger();
            
            builder.Logging.AddSerilog(dispose: true);

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

#if ANDROID
            builder.Services.AddSingleton<IDeviceDiscoveryService, AndroidDeviceDiscoveryService>();
            builder.Services.AddSingleton<ISerialPortCommunicator, AndroidUsbCommunicator>();
#elif WINDOWS
            builder.Services.AddSingleton<IDeviceDiscoveryService, DesktopDeviceDiscoveryService>();
            builder.Services.AddSingleton<ISerialPortCommunicator, WindowsDeviceCommunicator>();
#else
            builder.Services.AddSingleton<IDeviceDiscoveryService, TcpDeviceDiscoveryService>();
            builder.Services.AddSingleton<ISerialPortCommunicator, TcpDeviceCommunicator>(); 
#endif
            
#if ANDROID || WINDOWS
            builder.Services.AddSingleton<ITcpPortCommunicator, TcpDeviceCommunicator>();
#else
            builder.Services.AddSingleton<ITcpPortCommunicator>(sp => (ITcpPortCommunicator)sp.GetRequiredService<ISerialPortCommunicator>());
#endif
            
            builder.Services.AddSingleton<CommunicationMux>();
            builder.Services.AddSingleton<ConnectionStateService>();
            builder.Services.AddSingleton<ICustomViewService, CustomViewService>();
            builder.Services.AddSingleton<IAppSettingService, AppSettingService>();
            builder.Services.AddSingleton<IGpsService, GpsService>();
            builder.Services.AddSingleton<IGearCalculationService, GearCalculationService>();
            builder.Services.AddSingleton<ICompositeDataService, CompositeDataService>();
            
            builder.Services.AddSingleton<IEcuDefinitionService, EcuDefinitionService>();
            builder.Services.AddSingleton<IEcuInteractionService>(sp =>
                new EcuInteractionService(
                    sp.GetRequiredService<CommunicationMux>(),
                    sp.GetRequiredService<IEcuDefinitionService>(),
                    sp.GetRequiredService<ILogger<EcuInteractionService>>()));


            return builder.Build();
        }
    }
}