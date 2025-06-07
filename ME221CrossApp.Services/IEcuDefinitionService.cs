using ME221CrossApp.Models;

namespace ME221CrossApp.Services;

public interface IEcuDefinitionService
{
    Task LoadFromStoreAsync(CancellationToken cancellationToken = default);
    Task MergeDefinitionFileAsync(string xmlFilePath, CancellationToken cancellationToken = default);
    bool TryGetObject(ushort id, out EcuObjectDefinition? ecuObject);
    EcuDefinition? GetDefinition();
}