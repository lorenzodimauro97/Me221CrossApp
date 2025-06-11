namespace Me221CrossApp.UI.Services;

public static class ColorHelper
{
    public static string GetColorForValue(float value, float min, float max)
    {
        if (min >= max) return "hsl(240, 50%, 60%)";

        var normalizedValue = (value - min) / (max - min);
        
        var hue = (1.0f - normalizedValue) * 240;

        return $"hsl({hue:F0}, 70%, 60%)";
    }
}