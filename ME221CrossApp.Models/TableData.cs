namespace ME221CrossApp.Models;

public record TableData(
    ushort Id,
    string Name,
    IReadOnlyList<float> XAxis,
    IReadOnlyList<float> YAxis,
    IReadOnlyList<float> Output
);