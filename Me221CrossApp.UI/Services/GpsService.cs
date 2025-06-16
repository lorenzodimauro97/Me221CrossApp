using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Me221CrossApp.UI.Services;

public class GpsService(ILogger<GpsService> logger) : IGpsService
{
    private bool _isCheckingPermission;
    private PermissionStatus _permissionStatus = PermissionStatus.Unknown;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private double? _lastKnownSpeedKph;
    private DateTime _lastCheckTimestamp = DateTime.MinValue;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1);

    private async Task<bool> CheckAndRequestPermissionAsync()
    {
        if (_isCheckingPermission) return false;

        try
        {
            _isCheckingPermission = true;
            _permissionStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (_permissionStatus == PermissionStatus.Granted)
                return true;

            if (Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
            {
                logger.LogInformation("Showing rationale for location permission.");
            }

            _permissionStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            return _permissionStatus == PermissionStatus.Granted;
        }
        finally
        {
            _isCheckingPermission = false;
        }
    }

    public async Task<double?> GetCurrentSpeedKphAsync(CancellationToken cancellationToken = default)
    {
        if (DateTime.UtcNow.Subtract(_lastCheckTimestamp) < _checkInterval)
        {
            return _lastKnownSpeedKph;
        }

        if (!await _semaphore.WaitAsync(0, cancellationToken))
        {
            return _lastKnownSpeedKph;
        }

        try
        {
            _lastCheckTimestamp = DateTime.UtcNow;

            if (_permissionStatus != PermissionStatus.Granted)
            {
                if (!await CheckAndRequestPermissionAsync())
                {
                    logger.LogWarning("Location permission not granted. Cannot get speed.");
                    _lastKnownSpeedKph = null;
                    return null;
                }
            }
        
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5));
                var location = await Geolocation.Default.GetLocationAsync(request, cancellationToken);

                if (location?.Speed is not null)
                {
                    _lastKnownSpeedKph = location.Speed * 3.6;
                    return _lastKnownSpeedKph;
                }
            }
            catch (FeatureNotSupportedException)
            {
                logger.LogError("GPS not supported on this device.");
            }
            catch (FeatureNotEnabledException)
            {
                logger.LogError("GPS not enabled on this device.");
            }
            catch (PermissionException)
            {
                logger.LogError("GPS permission not granted.");
                _permissionStatus = PermissionStatus.Denied;
            }
            catch (Exception ex)
            {
                if (ex is not TaskCanceledException && ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "An unexpected error occurred while getting GPS location.");
                }
            }
            
            _lastKnownSpeedKph = null;
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}