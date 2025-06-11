using ME221CrossApp.EcuSimulator;
using ME221CrossApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    await Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console())
        .ConfigureServices((_, services) =>
        {
            services.AddSingleton<IEcuDefinitionService, EcuDefinitionService>();
            services.AddSingleton<ISimulatedEcuStateService, SimulatedEcuStateService>();
            services.AddHostedService<SimulatorHost>();
        })
        .RunConsoleAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}