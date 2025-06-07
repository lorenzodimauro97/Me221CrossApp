namespace ME221CrossApp.Models;

public record EcuInfo(
    string ProductName,
    string ModelName,
    string DefVersion,
    string FirmwareVersion,
    string Uuid,
    string Hash
);