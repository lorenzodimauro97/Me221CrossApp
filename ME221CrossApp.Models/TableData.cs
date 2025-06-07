namespace ME221CrossApp.Models;

public record TableData(
    ushort Id,
    string Name,
    byte TableType,
    bool Enabled,
    byte Rows,
    byte Cols,
    IReadOnlyList<float> XAxis,
    IReadOnlyList<float> YAxis,
    IReadOnlyList<float> Output
);