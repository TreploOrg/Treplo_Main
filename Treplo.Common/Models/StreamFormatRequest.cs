namespace Treplo.Common.Models;

[GenerateSerializer]
public readonly record struct StreamFormatRequest(uint? Channels, Container? Container, Codec? Codec, uint? Frequency);