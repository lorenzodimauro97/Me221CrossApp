using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using ME221CrossApp.Models;
using Microsoft.Extensions.Logging;

namespace ME221CrossApp.Services;

public class EcuDefinitionService : IEcuDefinitionService
{
    private EcuDefinition? _definition;
    private readonly string _definitionStorePath;
    private readonly ILogger<EcuDefinitionService> _logger;

    public event Action? OnDefinitionUpdated;

    public EcuDefinitionService(ILogger<EcuDefinitionService> logger)
    {
        _logger = logger;
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
        _logger.LogInformation("Loading ECU definitions from {StorePath}", _definitionStorePath);
        if (!File.Exists(_definitionStorePath))
        {
            _logger.LogWarning("Definition store file not found at {StorePath}", _definitionStorePath);
            _definition = new EcuDefinition("Store", "Not Initialized", "0", "", new Dictionary<ushort, EcuObjectDefinition>());
            return;
        }

        try
        {
            await using var storeStream = new FileStream(_definitionStorePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _definition = await JsonSerializer.DeserializeAsync<EcuDefinition>(storeStream, options, cancellationToken);
            if (_definition is null || _definition.EcuObjects is null || _definition.ProductName is null || _definition.DefVersion is null)
            {
                _logger.LogWarning("Failed to deserialize definitions from {StorePath}, file might be corrupt or incomplete.", _definitionStorePath);
                _definition = new EcuDefinition("Store", "Not Initialized", "0", "", new Dictionary<ushort, EcuObjectDefinition>());
            }
            else
            {
                _logger.LogInformation("Successfully loaded {ObjectCount} object definitions for '{ProductName}' version '{DefVersion}'.", _definition.EcuObjects.Count, _definition.ProductName, _definition.DefVersion);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while loading ECU definitions from {StorePath}", _definitionStorePath);
            _definition = new EcuDefinition("Store", "Not Initialized", "0", "", new Dictionary<ushort, EcuObjectDefinition>());
        }
    }

    public async Task MergeDefinitionFileAsync(string xmlFilePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Merging definition file {XmlFilePath}", xmlFilePath);
        try
        {
            var objects = new Dictionary<ushort, EcuObjectDefinition>();

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
            var defVersion = infoElement?.Element("FirmwareVersion")?.Value ?? version;

            var dataLinks = doc.Root?.Element("links")?.Elements("DataLinkModel") ?? [];
            foreach (var element in dataLinks)
            {
                if (ushort.TryParse(element.Element("id")?.Value, out var id))
                {
                    objects[id] = new EcuObjectDefinition(
                        Id: id,
                        Name: element.Element("name")?.Value ?? $"Unnamed Link {id}",
                        Category: element.Element("category")?.Value ?? "Uncategorized",
                        ObjectType: "DataLink",
                        Rows: null,
                        Cols: null,
                        Input0LinkId: null,
                        Input1LinkId: null,
                        Parameters: null,
                        InputLinks: null,
                        OutputLinks: null
                    );
                }
            }

            var drivers = doc.Root?.Element("drivers")?.Elements("DriverModel") ?? [];
            foreach (var element in drivers)
            {
                if (ushort.TryParse(element.Element("id")?.Value, out var id))
                {
                    var parameters = element.Elements("configParams").Elements("DriverModelParam")
                        .Select(p => new DriverParameterDefinition(
                            p.Element("name")?.Value ?? string.Empty,
                            p.Element("DisplayName")?.Value,
                            p.Element("SectionName")?.Value,
                            p.Element("ToolTipText")?.Value,
                            Enum.TryParse<DriverParameterType>(p.Element("type")?.Value, true, out var pType) ? pType : DriverParameterType.InputBox,
                            bool.TryParse(p.Element("readOnly")?.Value, out var ro) && ro,
                            bool.TryParse(p.Element("RequiresReset")?.Value, out var rr) && rr,
                            float.TryParse(p.Element("min")?.Value, CultureInfo.InvariantCulture, out var min) ? min : 0,
                            float.TryParse(p.Element("max")?.Value, CultureInfo.InvariantCulture, out var max) ? max : 0,
                            p.Element("options")?.Elements("comboBoxOption").Select(o => new ComboBoxOption(
                                ushort.TryParse(o.Element("id")?.Value, out var oId) ? oId : (ushort)0,
                                o.Element("name")?.Value ?? "Unnamed"
                            )).ToList(),
                            p.Element("ViewConstraint") != null ? new ViewConstraint(
                                int.TryParse(p.Element("ViewConstraint")?.Element("ParamIndex")?.Value, out var pcIdx) ? pcIdx : 0,
                                p.Element("ViewConstraint")?.Element("AcceptedValues")?.Elements("float").Select(v => float.TryParse(v.Value, CultureInfo.InvariantCulture, out var f) ? f : 0f).ToList() ?? []
                            ) : null
                        )).ToList();

                    var inputLinks = new DriverInputOutputDefinition(
                        bool.TryParse(element.Element("editableInputs")?.Value, out var ei) && ei,
                        element.Element("inputNames")?.Elements("string").Select(s => s.Value).ToList() ?? []
                    );

                    var outputLinks = new DriverInputOutputDefinition(
                        bool.TryParse(element.Element("editableOutputs")?.Value, out var eo) && eo,
                        element.Element("outputNames")?.Elements("string").Select(s => s.Value).ToList() ?? []
                    );

                    objects[id] = new EcuObjectDefinition(
                        Id: id,
                        Name: element.Element("name")?.Value ?? $"Unnamed Driver {id}",
                        Category: element.Element("category")?.Value ?? "Uncategorized",
                        ObjectType: "Driver",
                        Rows: null,
                        Cols: null,
                        Input0LinkId: null,
                        Input1LinkId: null,
                        Parameters: parameters,
                        InputLinks: inputLinks,
                        OutputLinks: outputLinks
                    );
                }
            }

            var tables = doc.Root?.Element("tables")?.Elements("TableModel") ?? [];
            foreach (var element in tables)
            {
                if (ushort.TryParse(element.Element("id")?.Value, out var id))
                {
                    byte? rows = null;
                    if (byte.TryParse(element.Element("rows")?.Value, out var r)) rows = r;

                    byte? cols = null;
                    if (byte.TryParse(element.Element("cols")?.Value, out var c)) cols = c;
                    
                    ushort? input0LinkId = null;
                    if (ushort.TryParse(element.Element("input_0_LinkId")?.Value, out var i0)) input0LinkId = i0;

                    ushort? input1LinkId = null;
                    if (ushort.TryParse(element.Element("input_1_LinkId")?.Value, out var i1)) input1LinkId = i1;

                    objects[id] = new EcuObjectDefinition(
                        Id: id,
                        Name: element.Element("name")?.Value ?? $"Unnamed Table {id}",
                        Category: element.Element("category")?.Value ?? "Uncategorized",
                        ObjectType: "Table",
                        Rows: rows,
                        Cols: cols,
                        Input0LinkId: input0LinkId,
                        Input1LinkId: input1LinkId,
                        Parameters: null,
                        InputLinks: null,
                        OutputLinks: null
                    );
                }
            }

            _definition = new EcuDefinition(productName, modelName, version, defVersion, objects);

            await using var writeStream = new FileStream(_definitionStorePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(writeStream, _definition, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
            _logger.LogInformation("Successfully merged and stored new definition from {XmlFilePath}", xmlFilePath);
            OnDefinitionUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge definition file {XmlFilePath}", xmlFilePath);
            throw;
        }
    }

    public Task ClearDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Clearing ECU definitions from {StorePath}", _definitionStorePath);
        _definition = new EcuDefinition("Store", "Not Initialized", "0", "", new Dictionary<ushort, EcuObjectDefinition>());
        if (File.Exists(_definitionStorePath))
        {
            try
            {
                File.Delete(_definitionStorePath);
                _logger.LogInformation("Deleted definition store file.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete definition store file at {StorePath}", _definitionStorePath);
            }
        }
        OnDefinitionUpdated?.Invoke();
        return Task.CompletedTask;
    }
}