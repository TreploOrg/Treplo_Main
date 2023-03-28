namespace Treplo.Common.OrleansGrpcConnector.Surrogates;

[GenerateSerializer]
public readonly struct Bitrate
{
    public Bitrate(ulong bitsPerSecond)
    {
        BitsPerSecond = bitsPerSecond;
    }
    
    [Id(0)] public readonly ulong BitsPerSecond;
}

[RegisterConverter]
public sealed class BitrateConverter : IConverter<Common.Bitrate, Bitrate>
{
    public Common.Bitrate ConvertFromSurrogate(in Bitrate surrogate) => new()
    {
        BitsPerSecond = surrogate.BitsPerSecond,
    };

    public Bitrate ConvertToSurrogate(in Common.Bitrate value) => new(value.BitsPerSecond);
}