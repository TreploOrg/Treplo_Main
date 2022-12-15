namespace Treplo.Common.Models;

public readonly record struct StreamFormatRequest(uint? Channels, Container? Container, Codec? Codec, uint? Frequency);