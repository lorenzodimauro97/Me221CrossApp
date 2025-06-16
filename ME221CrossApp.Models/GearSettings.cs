// File: ME221CrossApp.Models/GearSettings.cs
namespace ME221CrossApp.Models;

public class GearSettings
{
    public List<float> GearRatios { get; set; } = [3.626f, 2.200f, 1.541f, 1.213f, 1.000f, 0.767f];
    public float FinalDriveRatio { get; set; } = 4.1f;
    public float TireCircumferenceMeters { get; set; } = 1.83f;
    public double GearConfidenceThresholdKph { get; set; } = 15.0;
}