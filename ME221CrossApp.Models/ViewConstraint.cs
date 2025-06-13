namespace ME221CrossApp.Models;

public record ViewConstraint(
    int ParamIndex,
    IReadOnlyList<float> AcceptedValues
);