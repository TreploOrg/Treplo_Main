using Google.Protobuf.WellKnownTypes;

namespace Treplo.Common.OrleansGrpcConnector.Surrogates;

[GenerateSerializer]
public readonly struct Track
{
    public Track(
        string title,
        Thumbnail thumbnail,
        AudioSource source,
        string author,
        TimeSpan duration
    )
    {
        Title = title;
        Thumbnail = thumbnail;
        Source = source;
        Author = author;
        Duration = duration;
    }

    [Id(0)] public readonly string Title;
    [Id(1)] public readonly Thumbnail Thumbnail;
    [Id(2)] public readonly AudioSource Source;
    [Id(3)] public readonly string Author;
    [Id(4)] public readonly TimeSpan Duration;

    public override string ToString() => $"Track ({Title} - {Author})";
}

[RegisterConverter]
public sealed class TrackConverter : IConverter<Common.Track, Track>
{
    private readonly IConverter<Common.AudioSource, AudioSource> audioSourceConverter;
    private readonly IConverter<Common.Thumbnail, Thumbnail> thumbnailConverter;

    public TrackConverter(
        IConverter<Common.Thumbnail, Thumbnail> thumbnailConverter,
        IConverter<Common.AudioSource, AudioSource> audioSourceConverter
    )
    {
        this.thumbnailConverter = thumbnailConverter;
        this.audioSourceConverter = audioSourceConverter;
    }

    public Common.Track ConvertFromSurrogate(in Track surrogate) => new()
    {
        Title = surrogate.Title,
        Thumbnail = thumbnailConverter.ConvertFromSurrogate(in surrogate.Thumbnail),
        Author = surrogate.Author,
        Duration = Duration.FromTimeSpan(surrogate.Duration),
        Source = audioSourceConverter.ConvertFromSurrogate(in surrogate.Source),
    };

    public Track ConvertToSurrogate(in Common.Track value) => new(
        value.Title,
        thumbnailConverter.ConvertToSurrogate(value.Thumbnail),
        audioSourceConverter.ConvertToSurrogate(value.Source),
        value.Author,
        value.Duration.ToTimeSpan()
    );
}