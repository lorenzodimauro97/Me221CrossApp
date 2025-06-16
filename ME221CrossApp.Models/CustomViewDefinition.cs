namespace ME221CrossApp.Models;

public record CustomViewDefinition(Guid Id, string Name, string IconName, List<EcuObjectReference> EcuObjects)
{
    public string Name { get; set; } = Name;
    public string IconName { get; set; } = IconName;
    public List<EcuObjectReference> EcuObjects { get; set; } = EcuObjects;
}