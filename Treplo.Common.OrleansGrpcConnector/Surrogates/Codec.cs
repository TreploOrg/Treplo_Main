namespace Treplo.Common.OrleansGrpcConnector.Surrogates;

[GenerateSerializer]
public readonly struct Codec
{
    public Codec(string name)
    {
        Name = name;
    }

    [Id(0)] public readonly string Name;
}

[RegisterConverter]
public sealed class CodecConverter : IConverter<Common.Codec, Codec>
{
    public Common.Codec ConvertFromSurrogate(in Codec surrogate) => new()
    {
        Name = surrogate.Name,
    };

    public Codec ConvertToSurrogate(in Common.Codec value) => new(value.Name);
}