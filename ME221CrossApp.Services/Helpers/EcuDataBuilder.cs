// File: C:\Users\Administrator\RiderProjects\Me221CrossApp\ME221CrossApp.Services\Helpers\EcuDataBuilder.cs
using ME221CrossApp.Models;

namespace ME221CrossApp.Services.Helpers;

public static class EcuDataBuilder
{
    public static byte[] BuildSetTablePayload(TableData table)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        byte[] serializedTable = BuildSerializedTable(table);
        
        writer.Write(table.Id);
        writer.Write((ushort)serializedTable.Length);
        writer.Write(serializedTable);

        return stream.ToArray();
    }

    public static byte[] BuildSetDriverPayload(DriverData driver)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        byte[] serializedDriver = BuildSerializedDriver(driver);

        writer.Write(driver.Id);
        writer.Write((ushort)serializedDriver.Length);
        writer.Write(serializedDriver);

        return stream.ToArray();
    }
    
    private static byte[] BuildSerializedTable(TableData table)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(table.TableType);
        writer.Write(table.Enabled);
        writer.Write(table.Rows);
        writer.Write(table.Cols);

        if (table.Rows > 1)
        {
            foreach (var val in table.YAxis)
            {
                writer.Write(val);
            }
        }

        foreach (var val in table.XAxis)
        {
            writer.Write(val);
        }

        foreach (var val in table.Output)
        {
            writer.Write(val);
        }

        return stream.ToArray();
    }

    private static byte[] BuildSerializedDriver(DriverData driver)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)driver.ConfigParams.Count);
        writer.Write((byte)driver.OutputLinkIds.Count);
        writer.Write((byte)driver.InputLinkIds.Count);

        foreach (var val in driver.ConfigParams)
        {
            writer.Write(val);
        }

        foreach (var val in driver.OutputLinkIds)
        {
            writer.Write(val);
        }

        foreach (var val in driver.InputLinkIds)
        {
            writer.Write(val);
        }

        return stream.ToArray();
    }
}