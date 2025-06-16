using ME221CrossApp.Models;

namespace ME221CrossApp.UI.Services;

public interface IAppSettingService
{
    Task<GearSettings> GetGearSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveGearSettingsAsync(GearSettings settings, CancellationToken cancellationToken = default);
}