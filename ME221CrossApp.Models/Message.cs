namespace ME221CrossApp.Models;

public record Message(
    byte Type,
    byte Class,
    byte Command,
    byte[] Payload
);