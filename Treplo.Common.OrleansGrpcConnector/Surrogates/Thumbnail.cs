namespace Treplo.Common.OrleansGrpcConnector.Surrogates;

[GenerateSerializer]
public readonly struct Thumbnail
{
    public Thumbnail(string url)
    {
        Url = url;
    }

    [Id(0)] public readonly string Url;
}

[RegisterConverter]
public sealed class ThumbnailConverter : IConverter<Common.Thumbnail, Thumbnail>
{
    public Common.Thumbnail ConvertFromSurrogate(in Thumbnail surrogate) => new()
    {
        Url = surrogate.Url,
    };

    public Thumbnail ConvertToSurrogate(in Common.Thumbnail value) => new(value.Url);
}