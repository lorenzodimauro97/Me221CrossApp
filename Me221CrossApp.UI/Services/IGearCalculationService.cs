namespace Me221CrossApp.UI.Services;

public interface IGearCalculationService
{
    Task<(int? Gear, double? AssistedSpeedKph)> CalculateGearAndSpeedAsync(double gpsSpeedKph, double rpm, CancellationToken cancellationToken = default);
}