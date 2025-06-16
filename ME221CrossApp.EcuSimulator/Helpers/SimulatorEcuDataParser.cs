using ME221CrossApp.Models;
using ME221CrossApp.Services;

namespace ME221CrossApp.EcuSimulator.Helpers;

public static class SimulatorEcuDataParser
{
    public static TableData? ParseSetTablePayload(byte[] payload, IEcuDefinitionService? definitionService)
    {
        if (payload.Length < 4) return null;

        var id = BitConverter.ToUInt16(payload, 0);
        var size = BitConverter.ToUInt16(payload, 2);
        if (payload.Length < 4 + size) return null;

        var offset = 4;
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
    
        var name = definitionService?.TryGetObject(id, out var def) == true && def is not null ? def.Name : $"Table_{id}";
        return new TableData(id, name, tableType, enabled, rows, cols, xAxis, yAxis, output);
    }

    public static DriverData? ParseSetDriverPayload(byte[] payload, IEcuDefinitionService? definitionService)
    {
        if (payload.Length < 4) return null;

        var id = BitConverter.ToUInt16(payload, 0);
        var size = BitConverter.ToUInt16(payload, 2);
        if (payload.Length < 4 + size) return null;
    
        var offset = 4;
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

        var name = definitionService?.TryGetObject(id, out var def) == true && def is not null ? def.Name : $"Driver_{id}";
        return new DriverData(id, name, configParams, inputIds, outputIds);
    }
}