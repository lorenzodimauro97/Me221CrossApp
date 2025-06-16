using ME221CrossApp.Models;
using ME221CrossApp.UI.Services;

namespace Me221CrossApp.UI.Services;

public class GearCalculationService(IAppSettingService appSettingService) : IGearCalculationService
{
    private GearSettings? _settings;

    private async Task EnsureSettingsLoaded(CancellationToken cancellationToken)
    {
        _settings ??= await appSettingService.GetGearSettingsAsync(cancellationToken);
    }

    private static double CalculateSpeedKph(double rpm, double gearRatio, double finalDriveRatio, double tireCircumferenceMeters)
    {
        if (rpm <= 0 || gearRatio <= 0 || finalDriveRatio <= 0 || tireCircumferenceMeters <= 0)
        {
            return 0;
        }

        var wheelRpm = rpm / (gearRatio * finalDriveRatio);
        var wheelSpeedMps = wheelRpm / 60.0 * tireCircumferenceMeters;
        return wheelSpeedMps * 3.6;
    }

    public async Task<(int? Gear, double? AssistedSpeedKph)> CalculateGearAndSpeedAsync(double gpsSpeedKph, double rpm, CancellationToken cancellationToken = default)
    {
        await EnsureSettingsLoaded(cancellationToken);
        if (_settings is null || _settings.GearRatios.Count == 0)
        {
            return (null, null);
        }

        int? bestGear = null;
        var minDiff = double.MaxValue;

        for (var i = 0; i < _settings.GearRatios.Count; i++)
        {
            var gearRatio = _settings.GearRatios[i];
            var calculatedSpeed = CalculateSpeedKph(rpm, gearRatio, _settings.FinalDriveRatio, _settings.TireCircumferenceMeters);
            var diff = Math.Abs(calculatedSpeed - gpsSpeedKph);

            if (diff < minDiff)
            {
                minDiff = diff;
                bestGear = i + 1;
            }
        }

        if (bestGear.HasValue && minDiff < _settings.GearConfidenceThresholdKph)
        {
            var assistedSpeed = CalculateSpeedKph(rpm, _settings.GearRatios[bestGear.Value - 1], _settings.FinalDriveRatio, _settings.TireCircumferenceMeters);
            return (bestGear, assistedSpeed);
        }

        return (null, null);
    }
}