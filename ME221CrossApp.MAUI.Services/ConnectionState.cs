namespace ME221CrossApp.MAUI.Services;

public enum ConnectionMode
{
    Serial,
    Tcp
}

public class ConnectionState
{
    public ConnectionMode Mode { get; private set; }
    public bool IsModeSelected { get; private set; }
    public string? SelectedPort { get; private set; }

    public event Func<Task>? OnChangeAsync;

    public async Task SetModeAsync(ConnectionMode mode, string? port)
    {
        Mode = mode;
        SelectedPort = port;
        IsModeSelected = true;

        if (OnChangeAsync is not null)
        {
            await OnChangeAsync.Invoke();
        }
    }
}