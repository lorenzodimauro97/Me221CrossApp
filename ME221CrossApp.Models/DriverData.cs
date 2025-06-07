namespace ME221CrossApp.Models;

public record DriverData(
    ushort Id,
    string Name,
    IReadOnlyList<float> ConfigParams,
    IReadOnlyList<ushort> InputLinkIds,
    IReadOnlyList<ushort> OutputLinkIds
);