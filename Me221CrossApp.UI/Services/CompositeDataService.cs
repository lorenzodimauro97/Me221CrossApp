// File: ME221CrossApp.UI/Services/CompositeDataService.cs
using System.Runtime.CompilerServices;
using ME221CrossApp.Models;
using ME221CrossApp.Services;
using Microsoft.Extensions.Logging;

namespace Me221CrossApp.UI.Services;

public class CompositeDataService(
    IEcuInteractionService ecuService,
    IEcuDefinitionService definitionService,
    IGpsService gpsService,
    IGearCalculationService gearService,
    ILogger<CompositeDataService> logger)
    : ICompositeDataService
{
    private readonly IEcuDefinitionService _definitionService = definitionService;

    private const ushort GpsSpeedId = 65001;
    private const ushort GpsAssistedSpeedId = 65002;
    private const ushort CurrentGearId = 65003;

    private readonly List<EcuObjectDefinition> _virtualDataLinks =
    [
        new(GpsSpeedId, "GPS Speed", "GPS", "DataLink", null, null, null, null, null, null, null),
        new(GpsAssistedSpeedId, "GPS-Assisted Speed", "GPS", "DataLink", null, null, null, null, null, null, null),
        new(CurrentGearId, "Current Gear", "GPS", "DataLink", null, null, null, null, null, null, null)
    ];

    public async Task<IReadOnlyList<EcuObjectDefinition>> GetAvailableDataLinksAsync(CancellationToken cancellationToken = default)
    {
        var ecuDataLinks = await ecuService.GetDataLinkListAsync(false, cancellationToken);
        return ecuDataLinks.Concat(_virtualDataLinks).ToList();
    }

    public async Task<IReadOnlyList<EcuObjectDefinition>> GetAllEcuObjectDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var ecuObjects = await ecuService.GetObjectListAsync(cancellationToken);
        var ecuDataLinks = await ecuService.GetDataLinkListAsync(false, cancellationToken);
        return ecuObjects.Concat(ecuDataLinks).Concat(_virtualDataLinks).ToList();
    }

    public async IAsyncEnumerable<IReadOnlyList<RealtimeDataPoint>> StreamCompositeDataAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var dataPoints in ecuService.StreamRealtimeDataAsync(cancellationToken))
        {
            var augmentedData = new List<RealtimeDataPoint>(dataPoints);
            try
            {
                var gpsSpeed = await gpsService.GetCurrentSpeedKphAsync(cancellationToken);
                augmentedData.Add(new RealtimeDataPoint(GpsSpeedId, "GPS Speed", (float)(gpsSpeed ?? -1f)));

                var rpmDataPoint = dataPoints.FirstOrDefault(dp => dp.Name.Equals("RPM", StringComparison.OrdinalIgnoreCase));
                if (gpsSpeed.HasValue && rpmDataPoint is not null && rpmDataPoint.Value > 0)
                {
                    var (gear, assistedSpeed) = await gearService.CalculateGearAndSpeedAsync(gpsSpeed.Value, rpmDataPoint.Value, cancellationToken);
                    augmentedData.Add(new RealtimeDataPoint(CurrentGearId, "Current Gear", gear ?? 0));
                    augmentedData.Add(new RealtimeDataPoint(GpsAssistedSpeedId, "GPS-Assisted Speed", (float?)assistedSpeed ?? -1f));
                }
                else
                {
                    augmentedData.Add(new RealtimeDataPoint(CurrentGearId, "Current Gear", 0));
                    augmentedData.Add(new RealtimeDataPoint(GpsAssistedSpeedId, "GPS-Assisted Speed", -1f));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error augmenting real-time data with GPS info.");
                augmentedData.Add(new RealtimeDataPoint(GpsSpeedId, "GPS Speed", -1f));
                augmentedData.Add(new RealtimeDataPoint(CurrentGearId, "Current Gear", 0));
                augmentedData.Add(new RealtimeDataPoint(GpsAssistedSpeedId, "GPS-Assisted Speed", -1f));
            }
            
            yield return augmentedData;
        }
    }
}