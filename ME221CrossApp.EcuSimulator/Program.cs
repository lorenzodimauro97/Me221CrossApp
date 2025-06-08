using ME221CrossApp.EcuSimulator;
using ME221CrossApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<IEcuDefinitionService, EcuDefinitionService>();
        services.AddSingleton<ISimulatedEcuStateService, SimulatedEcuStateService>();
        services.AddHostedService<SimulatorHost>();
    })
    .RunConsoleAsync();