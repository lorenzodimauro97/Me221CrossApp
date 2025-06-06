using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO.Ports;
using System.Text.Json;
using ME221CrossApp.Models;
using ME221CrossApp.Services;
using ME221CrossApp.Services.Helpers;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<IDeviceCommunicator, DeviceCommunicator>();
        services.AddHostedService<ConsoleEcuHost>();
    })
    .RunConsoleAsync();

public class ConsoleEcuHost(
    IHostApplicationLifetime appLifetime,
    IConfiguration config,
    IDeviceCommunicator communicator)
    : IHostedService
{
    private List<Operation> _operations = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () =>
            {
                try
                {
                    LoadOperations();
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
                    appLifetime.StopApplication();
                }
            }, cancellationToken);
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    
    private void LoadOperations()
    {
        var json = File.ReadAllText("operations.json");
        _operations = JsonSerializer.Deserialize<List<Operation>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }
    
    private async Task RunMainLoop(CancellationToken token)
    {
        Console.WriteLine("--- .NET 9 ECU Communicator ---");
        
        var portName = SelectPort();
        if (string.IsNullOrEmpty(portName)) return;

        var baudRate = config.GetValue<int>("BaudRate");
        Console.WriteLine($"Connecting to {portName} at {baudRate} baud...");

        await communicator.ConnectAsync(portName, baudRate, token);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Connection successful.");
        Console.ResetColor();
        
        while (!token.IsCancellationRequested)
        {
            var operation = SelectOperation();
            if (operation is null) break;

            var payload = await BuildPayload(operation, token);
            
            var request = new Message(
                Type: 0x00, // REQ
                Class: Convert.ToByte(operation.MessageClass, 16),
                Command: Convert.ToByte(operation.MessageCommand, 16),
                Payload: payload
            );

            Console.WriteLine("\nSending request...");
            try
            {
                var response = await communicator.SendMessageAsync(request, TimeSpan.FromSeconds(5), token);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Response Received:");
                Console.WriteLine($"  Type: 0x{response.Type:X2}");
                Console.WriteLine($"  Class: 0x{response.Class:X2}");
                Console.WriteLine($"  Command: 0x{response.Command:X2}");
                Console.WriteLine($"  Payload: {BitConverter.ToString(response.Payload).Replace("-", " ")}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error during communication: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    private static string? SelectPort()
    {
        var portNames = SerialPort.GetPortNames();
        if (portNames.Length == 0)
        {
            Console.WriteLine("No serial ports found.");
            return null;
        }

        Console.WriteLine("\nAvailable serial ports:");
        for (var i = 0; i < portNames.Length; i++)
        {
            Console.WriteLine($"{i + 1}: {portNames[i]}");
        }
        
        Console.Write("Select a port: ");
        if (int.TryParse(Console.ReadLine(), out var choice) && choice > 0 && choice <= portNames.Length)
        {
            return portNames[choice - 1];
        }
        
        Console.WriteLine("Invalid selection.");
        return null;
    }

    private Operation? SelectOperation()
    {
        while (true)
        {
            Console.WriteLine("\nAvailable operations:");
            for (var i = 0; i < _operations.Count; i++)
            {
                Console.WriteLine($"{i + 1}: {_operations[i].Name} - {_operations[i].Description}");
            }

            Console.WriteLine("0: Exit");

            Console.Write("Select an operation: ");
            if (int.TryParse(Console.ReadLine(), out var choice) && choice >= 0 && choice <= _operations.Count)
            {
                return choice == 0 ? null : _operations[choice - 1];
            }

            Console.WriteLine("Invalid selection.");
        }
    }

    private static async Task<byte[]> BuildPayload(Operation operation, CancellationToken token)
    {
        using var ms = new MemoryStream();
        foreach (var param in operation.PayloadTemplate)
        {
            while (!token.IsCancellationRequested)
            {
                var defaultValueHint = param.DefaultValue is not null ? $" (default: {param.DefaultValue})" : "";
                Console.Write($"Enter value for '{param.Name}' ({param.Type}){defaultValueHint}: ");
                var input = await Console.In.ReadLineAsync(token) ?? "";
                
                if (string.IsNullOrEmpty(input) && param.DefaultValue is not null)
                {
                    input = param.DefaultValue.ToString()!;
                }

                if (PayloadConverter.TryConvert(input, param.Type, out var bytes))
                {
                    ms.Write(bytes);
                    break;
                }
                
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Invalid input. Please enter a valid '{param.Type}'.");
                Console.ResetColor();
            }
        }
        return ms.ToArray();
    }
}