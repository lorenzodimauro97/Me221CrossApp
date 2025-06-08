using System.Text.Json;
using System.Xml.Linq;
using ME221CrossApp.Models;

namespace ME221CrossApp.Services;

public class EcuDefinitionService : IEcuDefinitionService
{
    private EcuDefinition? _definition;
    private readonly string _definitionStorePath;

    public EcuDefinitionService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _definitionStorePath = Path.Combine(appDataPath, "ecu_definitions.json");
    }

    public EcuDefinition? GetDefinition() => _definition;

    public bool TryGetObject(ushort id, out EcuObjectDefinition? ecuObject)
    {
        ecuObject = null;
        return _definition?.EcuObjects.TryGetValue(id, out ecuObject) ?? false;
    }
    
    public async Task LoadFromStoreAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_definitionStorePath))
        {
            _definition = new EcuDefinition("Store", "Not Initialized", "0", new Dictionary<ushort, EcuObjectDefinition>());
            return;
        }

        await using var storeStream = new FileStream(_definitionStorePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var objects = await JsonSerializer.DeserializeAsync<Dictionary<ushort, EcuObjectDefinition>>(storeStream, options, cancellationToken);

        _definition = new EcuDefinition("Stored", "Definitions", "", objects ?? new Dictionary<ushort, EcuObjectDefinition>());
    }

    public async Task MergeDefinitionFileAsync(string xmlFilePath, CancellationToken cancellationToken = default)
    {
        Dictionary<ushort, EcuObjectDefinition> objects;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        if (File.Exists(_definitionStorePath))
        {
            await using var readStream = new FileStream(_definitionStorePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            objects = await JsonSerializer.DeserializeAsync<Dictionary<ushort, EcuObjectDefinition>>(readStream, options, cancellationToken)
                      ?? new Dictionary<ushort, EcuObjectDefinition>();
        }
        else
        {
            objects = new Dictionary<ushort, EcuObjectDefinition>();
        }
        
        var fileContent = await File.ReadAllTextAsync(xmlFilePath, cancellationToken);
        var startIndex = fileContent.IndexOf("<ecu>", StringComparison.OrdinalIgnoreCase);
        var endIndex = fileContent.LastIndexOf("</ecu>", StringComparison.OrdinalIgnoreCase);

        if (startIndex == -1 || endIndex == -1)
        {
            throw new InvalidDataException("The file does not contain a valid <ecu>...</ecu> block.");
        }

        var xmlData = fileContent.Substring(startIndex, endIndex - startIndex + "</ecu>".Length);
        using var stringReader = new StringReader(xmlData);
        var doc = await XDocument.LoadAsync(stringReader, LoadOptions.None, cancellationToken);

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
        
        await using var writeStream = new FileStream(_definitionStorePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await JsonSerializer.SerializeAsync(writeStream, objects, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }
}