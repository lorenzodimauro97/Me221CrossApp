using System.Text;
using ME221CrossApp.Models;
using ME221CrossApp.Services.Helpers;

namespace ME221CrossApp.EcuSimulator.Helpers;

public static class SimulatorEcuPayloadBuilder
{
    public static byte[] BuildGetEcuInfoResponsePayload(EcuInfo info)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write((byte)0x00); // Success status
        var payloadString = string.Join("\0", info.ProductName, info.ModelName, info.DefVersion, info.FirmwareVersion, info.Uuid, info.Hash) + "\0";
        writer.Write(Encoding.ASCII.GetBytes(payloadString));
        return ms.ToArray();
    }

    public static byte[] BuildGetObjectListResponsePayload(IReadOnlyList<EcuObjectDefinition> objects)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write((byte)0x00); // Success status
        writer.Write((byte)0x00); // Reserved
        writer.Write((byte)0x00); // Reserved
        writer.Write((byte)0x00); // Reserved
        writer.Write((ushort)objects.Count);
        foreach (var obj in objects)
        {
            writer.Write(obj.Id);
            writer.Write((ushort)0); // Placeholder for hash/version
        }
        return ms.ToArray();
    }

    public static byte[] BuildSetStateResponsePayload(IReadOnlyDictionary<ushort, (ushort Id, byte Type)> reportingMap)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write((byte)0x00); // Success status
        writer.Write((ushort)reportingMap.Count);
        foreach (var item in reportingMap.Values)
        {
            writer.Write(item.Id);
            writer.Write(item.Type);
        }
        return ms.ToArray();
    }

    public static byte[] BuildGetTableResponsePayload(TableData table)
    {
        var serializedTable = EcuDataBuilder.BuildSetTablePayload(table);
        var responsePayload = new byte[1 + serializedTable.Length];
        responsePayload[0] = 0x00; // Success
        Array.Copy(serializedTable, 0, responsePayload, 1, serializedTable.Length);
        return responsePayload;
    }

    public static byte[] BuildGetDriverResponsePayload(DriverData driver)
    {
        var serializedDriver = EcuDataBuilder.BuildSetDriverPayload(driver);
        var responsePayload = new byte[1 + serializedDriver.Length];
        responsePayload[0] = 0x00; // Success
        Array.Copy(serializedDriver, 0, responsePayload, 1, serializedDriver.Length);
        return responsePayload;
    }
    
    public static byte[] BuildRealtimeDataPayload(IReadOnlyCollection<RealtimeDataPoint> dataPoints, IReadOnlyDictionary<ushort, (ushort Id, byte Type)> reportingMap)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write((byte)0x00); // Success status

        var orderedDataPoints = reportingMap.Keys.Select(id => dataPoints.FirstOrDefault(dp => dp.Id == id)).ToList();

        foreach (var dp in orderedDataPoints)
        {
            if (dp == null) continue;

            var type = reportingMap[dp.Id].Type;
            switch (type)
            {
                case 0x00: writer.Write(dp.Value); break; // float
                case 0x01: writer.Write((short)dp.Value); break; // short
                case 0x02: writer.Write((ushort)dp.Value); break; // ushort
                case 0x03: writer.Write((sbyte)dp.Value); break; // sbyte
                case 0x04: writer.Write((byte)dp.Value); break; // byte
                case 0x05: writer.Write((byte)(dp.Value != 0 ? 1 : 0)); break; // bool
            }
        }
        return ms.ToArray();
    }
}