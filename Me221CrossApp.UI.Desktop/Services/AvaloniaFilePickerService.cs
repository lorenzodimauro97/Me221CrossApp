using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Me221CrossApp.UI.Shared.Services;

namespace Me221CrossApp.UI.Desktop.Services;

public class AvaloniaFilePickerService : IFilePickerService
{
    public async Task<string?> PickFileAsync(string title, IReadOnlyDictionary<string, string> fileTypes)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime || lifetime.MainWindow is null)
        {
            return null;
        }

        var patterns = fileTypes.Select(kvp => new FilePickerFileType(kvp.Key) { Patterns = [kvp.Value] }).ToList();

        var selectedFiles = await lifetime.MainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = title,
                FileTypeFilter = patterns
            });
        
        return selectedFiles.FirstOrDefault()?.TryGetLocalPath();
    }
}