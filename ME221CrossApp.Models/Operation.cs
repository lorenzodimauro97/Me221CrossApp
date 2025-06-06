namespace ME221CrossApp.Models;

public record Operation(
    string Name,
    string Description,
    string MessageClass,
    string MessageCommand,
    List<PayloadParameter> PayloadTemplate
);