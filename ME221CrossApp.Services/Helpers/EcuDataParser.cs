using ME221CrossApp.Models;

namespace ME221CrossApp.Services.Helpers;

public static class EcuDataParser
{
    public static EcuInfo ParseEcuInfo(byte[] payload)
    {
        var payloadString = System.Text.Encoding.ASCII.GetString(payload, 1, payload.Length - 1);
        var parts = payloadString.Split('\0');
        return new EcuInfo(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5]);
    }

    public static List<EcuObjectDefinition> ParseObjectList(byte[] payload, IEcuDefinitionService definitionService)
    {
        var list = new List<EcuObjectDefinition>();
        var entityCount = BitConverter.ToUInt16(payload, 4);
        var offset = 6;
        const int entitySize = 4;

        for (var i = 0; i < entityCount; i++)
        {
            if (offset + entitySize > payload.Length) break;
            
            var id = BitConverter.ToUInt16(payload, offset);
            if (definitionService.TryGetObject(id, out var def) && def is not null)
            {
                list.Add(def);
            }
            offset += entitySize;
        }
        return list;
    }
    
    public static List<(ushort Id, byte Type)> ParseSetStateResponse(byte[] payload)
    {
        var map = new List<(ushort, byte)>();
        if (payload.Length < 3 || payload[0] != 0) return map;

        var entityCount = BitConverter.ToUInt16(payload, 1);
        var offset = 3;
        const int entityDescriptionSize = 3;

        if (payload.Length < offset + (entityCount * entityDescriptionSize)) return map;

        for (var i = 0; i < entityCount; i++)
        {
            var id = BitConverter.ToUInt16(payload, offset);
            offset += 2;
            var type = payload[offset];
            offset += 1;
            map.Add((id, type));
        }
        return map;
    }
    
    public static List<RealtimeDataPoint> ParseRealtimeData(
        byte[] payload, 
        IReadOnlyList<(ushort Id, byte Type)> reportingMap,
        IEcuDefinitionService definitionService)
    {
        var results = new List<RealtimeDataPoint>();
        if (payload.Length < 1 || payload[0] != 0) return results;

        var offset = 1;
        foreach (var (id, type) in reportingMap)
        {
            var size = GetSizeForDataType(type);
            if (offset + size > payload.Length) break;

            var value = type switch
            {
                0x00 => BitConverter.ToSingle(payload, offset),
                0x01 => BitConverter.ToInt16(payload, offset),
                0x02 => BitConverter.ToUInt16(payload, offset),
                0x03 => (sbyte)payload[offset],
                0x04 => payload[offset],
                0x05 => payload[offset],
                _ => 0
            };
            offset += size;
            
            var name = definitionService.TryGetObject(id, out var def) && def is not null 
                ? def.Name 
                : $"ID_{id}";
            
            results.Add(new RealtimeDataPoint(id, name, value));
        }
        return results;
    }

    public static TableData? ParseTableData(byte[] payload, IEcuDefinitionService definitionService)
    {
        if (payload.Length < 5 || payload[0] != 0) return null;

        var id = BitConverter.ToUInt16(payload, 1);
        var size = BitConverter.ToUInt16(payload, 3);
        if (payload.Length < 5 + size) return null;

        var offset = 5;
        var tableType = payload[offset++];
        var enabled = payload[offset++] == 1;
        var rows = payload[offset++];
        var cols = payload[offset++];

        var yAxis = new float[rows > 1 ? rows : 0];
        if (rows > 1)
        {
            for (var i = 0; i < rows; i++)
            {
                yAxis[i] = BitConverter.ToSingle(payload, offset);
                offset += 4;
            }
        }

        var xAxis = new float[cols];
        for (var i = 0; i < cols; i++)
        {
            xAxis[i] = BitConverter.ToSingle(payload, offset);
            offset += 4;
        }

        var output = new float[rows * cols];
        for (var i = 0; i < rows * cols; i++)
        {
            output[i] = BitConverter.ToSingle(payload, offset);
            offset += 4;
        }
        
        var name = definitionService.TryGetObject(id, out var def) && def is not null ? def.Name : $"Table_{id}";
        return new TableData(id, name, tableType, enabled, rows, cols, xAxis, yAxis, output);
    }

    public static DriverData? ParseDriverData(byte[] payload, IEcuDefinitionService definitionService)
    {
        if (payload.Length < 5 || payload[0] != 0) return null;

        var id = BitConverter.ToUInt16(payload, 1);
        var size = BitConverter.ToUInt16(payload, 3);
        if (payload.Length < 5 + size) return null;
        
        var offset = 5;
        var numConfigs = payload[offset++];
        var numOutputs = payload[offset++];
        var numInputs = payload[offset++];
        
        var configParams = new float[numConfigs];
        for (var i = 0; i < numConfigs; i++)
        {
            configParams[i] = BitConverter.ToSingle(payload, offset);
            offset += 4;
        }
        
        var outputIds = new ushort[numOutputs];
        for (var i = 0; i < numOutputs; i++)
        {
            outputIds[i] = BitConverter.ToUInt16(payload, offset);
            offset += 2;
        }
        
        var inputIds = new ushort[numInputs];
        for (var i = 0; i < numInputs; i++)
        {
            inputIds[i] = BitConverter.ToUInt16(payload, offset);
            offset += 2;
        }

        var name = definitionService.TryGetObject(id, out var def) && def is not null ? def.Name : $"Driver_{id}";
        return new DriverData(id, name, configParams, inputIds, outputIds);
    }
    
    private static int GetSizeForDataType(byte type) => type switch
    {
        0x00 => 4, 0x01 => 2, 0x02 => 2, 0x03 => 1, 0x04 => 1, 0x05 => 1, _ => 0
    };
}