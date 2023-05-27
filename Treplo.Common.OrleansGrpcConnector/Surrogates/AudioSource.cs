namespace Treplo.Common.OrleansGrpcConnector.Surrogates;

[GenerateSerializer]
public readonly struct AudioSource
{
    public AudioSource(string url, Codec codec, Container container, Bitrate bitrate, Filesize filesize)
    {
        Url = url;
        Codec = codec;
        Container = container;
        Bitrate = bitrate;
        Filesize = filesize;
    }

    [Id(0)] public readonly string Url;
    [Id(1)] public readonly Codec Codec;
    [Id(2)] public readonly Container Container;
    [Id(3)] public readonly Bitrate Bitrate;
    [Id(4)] public readonly Filesize Filesize;
}

[RegisterConverter]
public sealed class AudioSourceConverter : IConverter<Common.AudioSource, AudioSource>
{
    private readonly IConverter<Common.Bitrate, Bitrate> bitrateConverter;
    private readonly IConverter<Common.Codec, Codec> codecConverter;
    private readonly IConverter<Common.Container, Container> containerConverter;
    private readonly IConverter<Common.Filesize, Filesize> filesizeConverter;

    public AudioSourceConverter(
        IConverter<Common.Bitrate, Bitrate> bitrateConverter,
        IConverter<Common.Codec, Codec> codecConverter,
        IConverter<Common.Container, Container> containerConverter,
        IConverter<Common.Filesize, Filesize> filesizeConverter
    )
    {
        this.bitrateConverter = bitrateConverter;
        this.codecConverter = codecConverter;
        this.containerConverter = containerConverter;
        this.filesizeConverter = filesizeConverter;
    }

    public Common.AudioSource ConvertFromSurrogate(in AudioSource surrogate) => new()
    {
        Url = surrogate.Url,
        Codec = codecConverter.ConvertFromSurrogate(in surrogate.Codec),
        Container = containerConverter.ConvertFromSurrogate(in surrogate.Container),
        Bitrate = bitrateConverter.ConvertFromSurrogate(in surrogate.Bitrate),
        Filesize = filesizeConverter.ConvertFromSurrogate(in surrogate.Filesize),
    };

    public AudioSource ConvertToSurrogate(in Common.AudioSource value) => new(
        value.Url,
        codecConverter.ConvertToSurrogate(value.Codec),
        containerConverter.ConvertToSurrogate(value.Container),
        bitrateConverter.ConvertToSurrogate(value.Bitrate),
        filesizeConverter.ConvertToSurrogate(value.Filesize)
    );
}