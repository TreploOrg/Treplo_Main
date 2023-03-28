namespace Treplo.Common.OrleansGrpcConnector.Surrogates;

[GenerateSerializer]
public readonly struct Filesize
{
    public Filesize(ulong bytesLength)
    {
        BytesLength = bytesLength;
    }

    [Id(0)] public readonly ulong BytesLength;
}

[RegisterConverter]
public sealed class FilesizeConverter : IConverter<Common.Filesize, Filesize>
{
    public Common.Filesize ConvertFromSurrogate(in Filesize surrogate) => new()
    {
        BytesLength = surrogate.BytesLength,
    };

    public Filesize ConvertToSurrogate(in Common.Filesize value) => new(value.BytesLength);
}