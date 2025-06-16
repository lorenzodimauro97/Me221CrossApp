using ME221CrossApp.Models;
using Microsoft.Extensions.Logging;

namespace ME221CrossApp.UI.Services;

public class ConnectionStateService(ILogger<ConnectionStateService> logger)
{
    public bool IsConnected { get; private set; }
    public EcuInfo? EcuInfo { get; private set; }
    public CommunicatorType CommunicatorType { get; private set; }
    public event Action? OnChange;

    public void SetConnectionState(bool isConnected, EcuInfo? ecuInfo, CommunicatorType type)
    {
        IsConnected = isConnected;
        EcuInfo = ecuInfo;
        CommunicatorType = type;
        logger.LogInformation("Connection state changed. IsConnected: {IsConnected}, ECU: {ProductName}, Type: {CommunicatorType}", isConnected, ecuInfo?.ProductName ?? "N/A", type);
        NotifyStateChanged();
    }
    
    public void Disconnect()
    {
        IsConnected = false;
        EcuInfo = null;
        logger.LogInformation("Connection state changed. IsConnected: false.");
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}