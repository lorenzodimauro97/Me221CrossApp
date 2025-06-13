namespace ME221CrossApp.Models;

public record CustomViewDefinition
{
    public Guid Id { get; init; }
    public string Name { get; set; }
    public string IconName { get; set; }
    public List<EcuObjectReference> EcuObjects { get; set; }

    public CustomViewDefinition(Guid id, string name, string iconName, List<EcuObjectReference> ecuObjects)
    {
        Id = id;
        Name = name;
        IconName = iconName;
        EcuObjects = ecuObjects;
    }
}