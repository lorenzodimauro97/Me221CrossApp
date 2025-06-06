namespace ME221CrossApp.Services.Helpers;

public static class PayloadConverter
{
    public static bool TryConvert(string value, string type, out byte[]? bytes)
    {
        bytes = null;
        try
        {
            bytes = type.ToLowerInvariant() switch
            {
                "byte" => [byte.Parse(value)],
                "ushort" => BitConverter.GetBytes(ushort.Parse(value)),
                "short" => BitConverter.GetBytes(short.Parse(value)),
                "uint" => BitConverter.GetBytes(uint.Parse(value)),
                "int" => BitConverter.GetBytes(int.Parse(value)),
                "float" => BitConverter.GetBytes(float.Parse(value)),
                "bool" => [bool.Parse(value) ? (byte)1 : (byte)0],
                "string" => System.Text.Encoding.ASCII.GetBytes(value),
                _ => null
            };

            return bytes is not null;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}