using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using ME221CrossApp.Models;
using ME221CrossApp.Services;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddSingleton<IDeviceCommunicator, DeviceCommunicator>();
        services.AddSingleton<IEcuDefinitionService, EcuDefinitionService>();
        services.AddSingleton<IEcuInteractionService, EcuInteractionService>();
        services.AddHostedService<ConsoleEcuHost>();
    })
    .RunConsoleAsync();

public class ConsoleEcuHost : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IConfiguration _config;
    private readonly IDeviceCommunicator _communicator;
    private readonly IEcuDefinitionService _definitionService;
    private readonly IEcuInteractionService _ecuInteractionService;
    private List<Operation> _operations = [];

    public ConsoleEcuHost(
        IHostApplicationLifetime appLifetime, 
        IConfiguration config,
        IDeviceCommunicator communicator,
        IEcuDefinitionService definitionService,
        IEcuInteractionService ecuInteractionService)
    {
        _appLifetime = appLifetime;
        _config = config;
        _communicator = communicator;
        _definitionService = definitionService;
        _ecuInteractionService = ecuInteractionService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () =>
            {
                try
                {
                    await RunMainLoop(cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    Console.ResetColor();
                }
                finally
                {
                    _appLifetime.StopApplication();
                }
            }, cancellationToken);
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void LoadOperations()
    {
        var json = File.ReadAllText("operations.json");
        _operations = JsonSerializer.Deserialize<List<Operation>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }
    
    private async Task RunMainLoop(CancellationToken token)
    {
        Console.WriteLine("--- .NET 9 ECU Communicator ---");
        await _definitionService.LoadFromStoreAsync(token);
        var initialDef = _definitionService.GetDefinition();
        Console.WriteLine($"Loaded {initialDef?.EcuObjects.Count ?? 0} definitions from local store.");

        LoadOperations();
        
        var portName = SelectPort();
        if (string.IsNullOrEmpty(portName)) return;

        var baudRate = _config.GetValue<int>("BaudRate");
        Console.WriteLine($"Connecting to {portName} at {baudRate} baud...");

        await _communicator.ConnectAsync(portName, baudRate, token);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Connection successful.");
        Console.ResetColor();
        
        while (!token.IsCancellationRequested)
        {
            var operation = SelectOperation();
            if (operation is null) break;

            try
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\nExecuting: {operation.Name}...");
                Console.ResetColor();

                switch (operation.Name)
                {
                    case "Get ECU Info":
                        await GetEcuInfoAsync(token);
                        break;
                    case "Get Object List (Tables/Drivers)":
                        await GetObjectListAsync(token);
                        break;
                    case "Get DataLink List":
                        await GetDataLinkListAsync(token);
                        break;
                    case "Get Single DataLink Value":
                        await GetSingleDataLinkValueAsync(token);
                        break;
                    case "Get Table by ID":
                        await GetTableByIdAsync(token);
                        break;
                    case "Get Driver by ID":
                        await GetDriverByIdAsync(token);
                        break;
                    case "Start Real-time Data Stream":
                        await StreamRealtimeDataAsync(token);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Operation failed: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    private async Task GetEcuInfoAsync(CancellationToken token)
    {
        var info = await _ecuInteractionService.GetEcuInfoAsync(token);
        if (info is not null)
        {
            Console.WriteLine($"  Product: {info.ProductName}");
            Console.WriteLine($"  Model: {info.ModelName}");
            Console.WriteLine($"  DEF Version: {info.DefVersion}");
            Console.WriteLine($"  Firmware: {info.FirmwareVersion}");
            Console.WriteLine($"  UUID: {info.Uuid}");
            Console.WriteLine($"  Hash: {info.Hash}");
        }
        else
        {
            Console.WriteLine("  Could not retrieve ECU info.");
        }
    }

    private async Task GetObjectListAsync(CancellationToken token)
    {
        var objects = await _ecuInteractionService.GetObjectListAsync(token);
        Console.WriteLine($"  Found {objects.Count} objects:");
        foreach (var obj in objects)
        {
            Console.WriteLine($"    ID: {obj.Id,-5} | Name: {obj.Name,-35} | Type: {obj.ObjectType}");
        }
    }

    private async Task GetDataLinkListAsync(CancellationToken token)
    {
        var dataLinks = await _ecuInteractionService.GetDataLinkListAsync(token);
        Console.WriteLine($"  Found {dataLinks.Count} datalinks:");
        foreach (var link in dataLinks)
        {
            Console.WriteLine($"    ID: {link.Id,-5} | Name: {link.Name,-35}");
        }
    }
    
    private async Task GetSingleDataLinkValueAsync(CancellationToken token)
    {
        Console.Write("Enter DataLink ID to read: ");
        if (ushort.TryParse(Console.ReadLine(), out var id))
        {
            var dataPoint = await _ecuInteractionService.GetRealtimeDataValueAsync(id, token);
            if (dataPoint is not null)
            {
                Console.WriteLine($"  Value for {dataPoint.Name} (ID: {id}): {dataPoint.Value:F2}");
            }
            else
            {
                Console.WriteLine($"  Could not retrieve value for DataLink ID: {id}. It may not be in the reporting map.");
            }
        }
        else
        {
            Console.WriteLine("  Invalid ID.");
        }
    }

    private async Task GetTableByIdAsync(CancellationToken token)
    {
        Console.Write("Enter Table ID to read: ");
        if (ushort.TryParse(Console.ReadLine(), out var id))
        {
            var table = await _ecuInteractionService.GetTableAsync(id, token);
            if (table is not null)
            {
                Console.WriteLine($"  Table: {table.Name} (ID: {id})");
                Console.WriteLine($"  X-Axis: [{string.Join(", ", table.XAxis)}]");
                if(table.YAxis.Any()) Console.WriteLine($"  Y-Axis: [{string.Join(", ", table.YAxis)}]");
                Console.WriteLine($"  Output: [{string.Join(", ", table.Output.Select(v => v.ToString("F2")))}]");
            }
            else
            {
                Console.WriteLine($"  Could not retrieve table with ID: {id}.");
            }
        }
        else
        {
            Console.WriteLine("  Invalid ID.");
        }
    }
    
    private async Task GetDriverByIdAsync(CancellationToken token)
    {
        Console.Write("Enter Driver ID to read: ");
        if (ushort.TryParse(Console.ReadLine(), out var id))
        {
            var driver = await _ecuInteractionService.GetDriverAsync(id, token);
            if (driver is not null)
            {
                Console.WriteLine($"  Driver: {driver.Name} (ID: {id})");
                Console.WriteLine($"  Config Params: [{string.Join(", ", driver.ConfigParams.Select(v => v.ToString("F2")))}]");
                Console.WriteLine($"  Input Links: [{string.Join(", ", driver.InputLinkIds)}]");
                Console.WriteLine($"  Output Links: [{string.Join(", ", driver.OutputLinkIds)}]");
            }
            else
            {
                Console.WriteLine($"  Could not retrieve driver with ID: {id}.");
            }
        }
        else
        {
            Console.WriteLine("  Invalid ID.");
        }
    }

    private async Task StreamRealtimeDataAsync(CancellationToken token)
    {
        Console.WriteLine("Starting real-time stream... Press Enter to stop.");
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        
        var keepAliveTask = Task.Run(async () =>
        {
            var ackMessage = new Message(0x0F, 0x00, 0x01, [0x00]);
            while (!linkedCts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000, linkedCts.Token);
                await _communicator.PostMessageAsync(ackMessage, linkedCts.Token);
            }
        }, linkedCts.Token);

        var streamTask = Task.Run(async () =>
        {
            await foreach (var dataPoints in _ecuInteractionService.StreamRealtimeDataAsync(linkedCts.Token))
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("--- Real-time Data Stream (Press Enter to stop) ---");
                foreach (var dp in dataPoints)
                {
                    Console.WriteLine($"  {dp.Name,-35} (ID: {dp.Id,-5}): {dp.Value,10:F2}");
                }
                Console.ResetColor();
            }
        }, linkedCts.Token);

        await Task.WhenAny(streamTask, Console.In.ReadLineAsync(linkedCts.Token).AsTask());
        await linkedCts.CancelAsync();
        
        Console.Clear();
        Console.WriteLine("\nReal-time stream stopped.");
    }
    
    private string? SelectPort()
    {
        var portNames = SerialPort.GetPortNames();
        if (portNames.Length == 0)
        {
            Console.WriteLine("No serial ports found.");
            return null;
        }

        Console.WriteLine("\nAvailable serial ports:");
        for (int i = 0; i < portNames.Length; i++)
        {
            Console.WriteLine($"{i + 1}: {portNames[i]}");
        }
        
        Console.Write("Select a port: ");
        if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= portNames.Length)
        {
            return portNames[choice - 1];
        }
        
        Console.WriteLine("Invalid selection.");
        return null;
    }
    
    private Operation? SelectOperation()
    {
        Console.WriteLine("\nAvailable operations:");
        for (int i = 0; i < _operations.Count; i++)
        {
            Console.WriteLine($"{i + 1}: {_operations[i].Name}");
        }
        Console.WriteLine("0: Exit");

        Console.Write("Select an operation: ");
        if (int.TryParse(Console.ReadLine(), out int choice) && choice >= 0 && choice <= _operations.Count)
        {
            if (choice == 0) return null;
            return _operations[choice - 1];
        }

        Console.WriteLine("Invalid selection.");
        return SelectOperation();
    }
}