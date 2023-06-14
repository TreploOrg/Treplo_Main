using Discord;
using Treplo.Common;
using Treplo.PlayersService;

namespace Treplo;

public interface ITrackEmbedBuilder
{
    EmbedBuilder ForTrack(Track track);
    IEnumerable<EmbedBuilder> ForState((Track, TimeSpan)? current, PlayerState playerState);
}

public class TrackEmbedBuilder : ITrackEmbedBuilder
{
    private const int EmbedColor = 0x0099FF;

    public EmbedBuilder ForTrack(Track track)
        => new EmbedBuilder().WithColor(EmbedColor)
            .WithCurrentTimestamp()
            .WithTitle(track.Title)
            .WithFields(
                new EmbedFieldBuilder().WithName("Author").WithValue(track.Author),
                new EmbedFieldBuilder().WithName("Duration").WithValue(track.Duration.ToTimeSpan().ToString("g"))
            )
            .WithImageUrl(track.Thumbnail.Url);

    public IEnumerable<EmbedBuilder> ForState((Track, TimeSpan)? current, PlayerState playerState)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var totalPlayTime =
            playerState.Tracks.Aggregate(TimeSpan.Zero, (seed, track) => seed + track.Duration.ToTimeSpan());

        {
            if (current is var (track, playTime))
                totalPlayTime += track.Duration.ToTimeSpan() - playTime;
        }

        yield return new EmbedBuilder().WithColor(EmbedColor)
            .WithTimestamp(timestamp)
            .WithTitle("Player state")
            .AddField(x => x.WithName("Loop").WithValue(playerState.Loop.ToString()).WithIsInline(true))
            .AddField(x => x.WithName("Tracks in queue").WithValue(playerState.Tracks.Length.ToString()).WithIsInline(true))
            .AddField(x => x.WithName("Total queue play time").WithValue(totalPlayTime.ToString("g")));

        {
            if (current is var (currentTrack, playTime))
                yield return GetForCurrent(currentTrack, playTime, timestamp);
        }

        if (playerState.Tracks.Length == 0)
            yield break;

        var queueEmbed = new EmbedBuilder().WithColor(EmbedColor)
            .WithTimestamp(timestamp)
            .WithTitle("Queue");
        for (var i = 0; i < playerState.Tracks.Length; i++)
        {
            var track = playerState.Tracks[i];
            var name = (i + 1).ToString();
            var value = $"{track.Title} - {track.Author}. {track.Duration.ToTimeSpan():g}";
            queueEmbed.AddField(name, value);
        }

        yield return queueEmbed;
    }

    private EmbedBuilder GetForCurrent(Track track, TimeSpan playTime, DateTimeOffset timestamp)
        => new EmbedBuilder().WithColor(EmbedColor)
            .WithTimestamp(timestamp)
            .WithTitle($"Currently playing - {track.Title}")
            .AddField(
                x => x.WithName("Play time")
                    .WithValue($"{playTime:g}/{track.Duration.ToTimeSpan():g}")
            )
            .AddField(x => x.WithName("Author").WithValue(track.Author))
            .WithImageUrl(track.Thumbnail.Url);
}