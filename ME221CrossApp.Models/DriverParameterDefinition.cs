namespace ME221CrossApp.Models;

public record DriverParameterDefinition(
    string Name,
    string? DisplayName,
    string? SectionName,
    string? ToolTipText,
    DriverParameterType Type,
    bool IsReadOnly,
    bool RequiresReset,
    float MinValue,
    float MaxValue,
    IReadOnlyList<ComboBoxOption>? Options,
    ViewConstraint? ViewConstraint
);