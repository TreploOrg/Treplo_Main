using Treplo.Common;

namespace Treplo.Converters;

public readonly record struct StreamFormatRequest(uint? Channels, Container? Container, Codec? Codec, uint? Frequency);