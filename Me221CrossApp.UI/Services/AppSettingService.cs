using System.Text.Json;
using ME221CrossApp.Models;
using Microsoft.Extensions.Logging;

namespace ME221CrossApp.UI.Services;

public class AppSettingService(ILogger<AppSettingService> logger) : IAppSettingService
{
    private readonly string _gearSettingsPath = Path.Combine(FileSystem.AppDataDirectory, "gear_settings.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<GearSettings> GetGearSettingsAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_gearSettingsPath))
            {
                return new GearSettings();
            }

            try
            {
                await using var stream = File.OpenRead(_gearSettingsPath);
                return await JsonSerializer.DeserializeAsync<GearSettings>(stream, Options, cancellationToken) ?? new GearSettings();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load gear settings from {Path}", _gearSettingsPath);
                return new GearSettings();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveGearSettingsAsync(GearSettings settings, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(settings, Options);
            await File.WriteAllTextAsync(_gearSettingsPath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save gear settings to {Path}", _gearSettingsPath);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}