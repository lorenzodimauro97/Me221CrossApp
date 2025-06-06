namespace ME221CrossApp.Services.Helpers;

public static class Fletcher16Checksum
{
    public static ushort Compute(byte[] data)
    {
        ushort a = 0;
        ushort b = 0;

        foreach (var t in data)
        {
            a = (ushort)((a + t) % 255);
            b = (ushort)((b + a) % 255);
        }
        return (ushort)((b << 8) | a);
    }
}