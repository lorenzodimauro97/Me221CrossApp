namespace Me221CrossApp.UI.Services;

public interface IGpsService
{
    Task<double?> GetCurrentSpeedKphAsync(CancellationToken cancellationToken = default);
}