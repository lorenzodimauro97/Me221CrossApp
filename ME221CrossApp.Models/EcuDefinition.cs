namespace ME221CrossApp.Models;

public record EcuDefinition(
    string ProductName,
    string ModelName,
    string Version,
    string DefVersion,
    IReadOnlyDictionary<ushort, EcuObjectDefinition> EcuObjects
);

public record EcuObjectDefinition(
    ushort Id,
    string Name,
    string Category,
    string ObjectType,
    byte? Rows,
    byte? Cols,
    ushort? Input0LinkId,
    ushort? Input1LinkId,
    IReadOnlyList<DriverParameterDefinition>? Parameters,
    DriverInputOutputDefinition? InputLinks,
    DriverInputOutputDefinition? OutputLinks
);