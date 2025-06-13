namespace ME221CrossApp.Models;

public record DriverInputOutputDefinition(
    bool IsEditable,
    IReadOnlyList<string> Names
);