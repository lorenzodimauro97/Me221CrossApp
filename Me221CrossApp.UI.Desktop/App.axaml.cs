using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using ME221CrossApp.Services;
using Me221CrossApp.UI.Desktop.Services;
using Me221CrossApp.UI.Desktop.ViewModels;
using Me221CrossApp.UI.Desktop.Views;
using Me221CrossApp.UI.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Me221CrossApp.UI.Desktop;

public partial class App : Application
{
    public static IHost? AppHost { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal static void RunAvaloniaAppWithHosting(string[] args, Func<AppBuilder> buildAvaloniaApp)
    {
        var appBuilder = Host.CreateApplicationBuilder(args);
        appBuilder.Logging.AddDebug();
        appBuilder.Services.AddWindowsFormsBlazorWebView();
        appBuilder.Services.AddBlazorWebViewDeveloperTools();

        appBuilder.Services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        appBuilder.Services.AddSingleton<IEcuDefinitionService, EcuDefinitionService>();
        
        using var myApp = appBuilder.Build();
        App.AppHost = myApp;

        myApp.Start();

        try
        {
            buildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.ToString());
        }
        finally
        {
            Task.Run(async () => await myApp.StopAsync()).GetAwaiter().GetResult();
        }
    }
}