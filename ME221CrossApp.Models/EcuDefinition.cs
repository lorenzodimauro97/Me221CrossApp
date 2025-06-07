namespace ME221CrossApp.Models;

public record EcuDefinition(
    string ProductName,
    string ModelName,
    string Version,
    IReadOnlyDictionary<ushort, EcuObjectDefinition> EcuObjects
);

public record EcuObjectDefinition(
    ushort Id,
    string Name,
    string Category,
    string ObjectType
);