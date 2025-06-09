namespace Me221CrossApp.UI.Shared.Services;

public interface IFilePickerService
{
    Task<string?> PickFileAsync(string title, IReadOnlyDictionary<string, string> fileTypes);
}