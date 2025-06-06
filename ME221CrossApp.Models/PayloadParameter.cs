namespace ME221CrossApp.Models;

public record PayloadParameter(
    string Name,
    string Type,
    object? DefaultValue = null
);