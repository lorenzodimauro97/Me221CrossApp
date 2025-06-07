using System.Text.Json;
using System.Xml.Linq;
using ME221CrossApp.Models;

namespace ME221CrossApp.Services;

public class EcuDefinitionService : IEcuDefinitionService
{
    private EcuDefinition? _definition;
    private const string DefinitionStorePath = "ecu_definitions.json";

    public EcuDefinition? GetDefinition() => _definition;

    public bool TryGetObject(ushort id, out EcuObjectDefinition? ecuObject)
    {
        ecuObject = null;
        return _definition?.EcuObjects.TryGetValue(id, out ecuObject) ?? false;
    }
    
    public async Task LoadFromStoreAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(DefinitionStorePath))
        {
            _definition = new EcuDefinition("Store", "Not Initialized", "0", new Dictionary<ushort, EcuObjectDefinition>());
            return;
        }

        await using var storeStream = new FileStream(DefinitionStorePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var objects = await JsonSerializer.DeserializeAsync<Dictionary<ushort, EcuObjectDefinition>>(storeStream, options, cancellationToken);

        _definition = new EcuDefinition("Stored", "Definitions", "", objects ?? new Dictionary<ushort, EcuObjectDefinition>());
    }

    public async Task MergeDefinitionFileAsync(string xmlFilePath, CancellationToken cancellationToken = default)
    {
        Dictionary<ushort, EcuObjectDefinition> objects;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        if (File.Exists(DefinitionStorePath))
        {
            await using var readStream = new FileStream(DefinitionStorePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            objects = await JsonSerializer.DeserializeAsync<Dictionary<ushort, EcuObjectDefinition>>(readStream, options, cancellationToken)
                      ?? new Dictionary<ushort, EcuObjectDefinition>();
        }
        else
        {
            objects = new Dictionary<ushort, EcuObjectDefinition>();
        }
        
        await using var stream = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);

        var infoElement = doc.Root?.Element("DeviceDataInformationModel");
        var productName = infoElement?.Element("ProductName")?.Value ?? "Unknown";
        var modelName = infoElement?.Element("ModelName")?.Value ?? "Unknown";
        var version = infoElement?.Element("Version")?.Value ?? "0";

        var dataLinks = doc.Root?.Element("links")?.Elements("DataLinkModel") ?? [];
        foreach (var element in dataLinks)
        {
            if (ushort.TryParse(element.Element("id")?.Value, out var id))
            {
                objects[id] = new EcuObjectDefinition(
                    Id: id,
                    Name: element.Element("name")?.Value ?? $"Unnamed Link {id}",
                    Category: element.Element("category")?.Value ?? "Uncategorized",
                    ObjectType: "DataLink"
                );
            }
        }
        
        var drivers = doc.Root?.Element("drivers")?.Elements("DriverModel") ?? [];
        foreach (var element in drivers)
        {
            if (ushort.TryParse(element.Element("id")?.Value, out var id))
            {
                objects[id] = new EcuObjectDefinition(
                    Id: id,
                    Name: element.Element("name")?.Value ?? $"Unnamed Driver {id}",
                    Category: element.Element("category")?.Value ?? "Uncategorized",
                    ObjectType: "Driver"
                );
            }
        }
        
        var tables = doc.Root?.Element("tables")?.Elements("TableModel") ?? [];
        foreach (var element in tables)
        {
            if (ushort.TryParse(element.Element("id")?.Value, out var id))
            {
                objects[id] = new EcuObjectDefinition(
                    Id: id,
                    Name: element.Element("name")?.Value ?? $"Unnamed Table {id}",
                    Category: element.Element("category")?.Value ?? "Uncategorized",
                    ObjectType: "Table"
                );
            }
        }
        
        _definition = new EcuDefinition(productName, modelName, version, objects);
        
        await using var writeStream = new FileStream(DefinitionStorePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await JsonSerializer.SerializeAsync(writeStream, objects, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }
}