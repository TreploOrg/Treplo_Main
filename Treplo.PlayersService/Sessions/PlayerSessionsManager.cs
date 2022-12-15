using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Threading.Tasks.Sources;
using CliWrap;
using Treplo.Common.Models;
using Treplo.PlayersService.Converters;

namespace Treplo.PlayersService.Sessions;

public sealed class PlayerSessionsManager
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<PlayerSessionsManager> logger;
    private readonly ILogger<Session> sessionLogger;
    private readonly FfmpegFactory ffmpegFactory;

    private readonly ConcurrentDictionary<ulong, Session> sessions = new();

    public PlayerSessionsManager(
        IHttpClientFactory httpClientFactory,
        FfmpegFactory ffmpegFactory,
        ILogger<PlayerSessionsManager> logger,
        ILogger<Session> sessionLogger
    )
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
        this.sessionLogger = sessionLogger;
        this.ffmpegFactory = ffmpegFactory;
    }

    //TODO: New error types
    public ValueTask<PipeReader?> PlayAsync(
        ulong sessionId
    )
    {
        if (!sessions.TryGetValue(sessionId, out var session))
            throw new Exception();

        return ValueTask.FromResult(session.StartPlayback());
    }

    public ValueTask<Guid> StartSearchAsync(ulong sessionId, TrackRequest[] tracks)
    {
        var session = GetSession(sessionId);

        return ValueTask.FromResult(session.StartSearchSession(tracks));
    }

    private Session GetSession(ulong sessionId)
    {
        return sessions.GetOrAdd(sessionId,
            static (sessionId, arg) => new Session(sessionId, arg.ffmpegFactory, arg.httpClientFactory, arg.sessionLogger),
            (ffmpegFactory, httpClientFactory, sessionLogger));
    }

    public ValueTask RespondToSearchAsync(
        ulong sessionId,
        Guid searchId,
        uint searchResultIndex
    )
    {
        if (!sessions.TryGetValue(sessionId, out var session))
            throw new Exception();

        session.RespondToSearch(searchId, searchResultIndex);

        return ValueTask.CompletedTask;
    }

    public ValueTask EnqueueAsync(ulong sessionId, TrackRequest requestTrack)
    {
        var session = GetSession(sessionId);
        session.Enqueue(requestTrack);
        return ValueTask.CompletedTask;
    }
}

public class Session
{
    private readonly ConcurrentDictionary<Guid, TrackRequest[]> searchSessions = new();
    private readonly ConcurrentQueue<TrackRequest> trackQueue = new();
    private readonly ulong sessionId;
    private readonly FfmpegFactory ffmpegFactory;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<Session> logger;

    private Task? currentPlaybackTask;
    private CancellationTokenSource cts = new();

    public Session(
        ulong sessionId,
        FfmpegFactory ffmpegFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<Session> logger
    )
    {
        this.sessionId = sessionId;
        this.ffmpegFactory = ffmpegFactory;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    public PipeReader? StartPlayback()
    {
        if (currentPlaybackTask is not null)
        {
            return null;
        }

        if (trackQueue.IsEmpty)
            return null;

        var pipe = new Pipe();
        currentPlaybackTask = Core(pipe);

        return pipe.Reader;

        async Task Core(Pipe pipe)
        {
            Exception? localException = null;
            try
            {
                while (true)
                {
                    if (GetNext() is not { Track: {Track: var track, StreamFormatRequest: var streamRequest}})
                        break;

                    using var client = httpClientFactory.CreateClient(nameof(Session));
                    await using var inStream = await client.GetStreamAsync(track.Source.Url, cts.Token);
                    var ffmpeg = ffmpegFactory.Create(track.Source, streamRequest);

                    await Task.WhenAll(
                        inStream.CopyToAsync(ffmpeg.Input, cts.Token).ContinueWith(task => ffmpeg.Input.CompleteAsync(task.Exception)),
                        ffmpeg.StartAsync(cts.Token),
                        ffmpeg.Output.CopyToAsync(pipe.Writer, cts.Token)
                    );
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Playback in {SessionId} was canceled", sessionId);
            }
            catch (Exception e)
            {
                localException = e;
                logger.LogError(e, "Exception occured during playback in {SessionId}", sessionId);
            }
            finally
            {
                logger.LogInformation("Playback in {SessionId} ended", sessionId);
                await pipe.Writer.FlushAsync(cts.Token);
                await pipe.Writer.CompleteAsync(localException);
                currentPlaybackTask = null;
            }
        }
    }

    private CurrentTrack? GetNext()
    {
        if (trackQueue.TryDequeue(out var trackRequest))
            return new CurrentTrack(trackRequest, DateTime.UtcNow, null);

        return null;
    }

    public Guid StartSearchSession(TrackRequest[] tracks)
    {
        var guid = Guid.NewGuid();
        searchSessions[guid] = tracks;
        return guid;
    }

    public void RespondToSearch(Guid searchId, uint searchResultIndex)
    {
        if (!searchSessions.TryRemove(searchId, out var tracks))
            throw new Exception();

        if (tracks.Length <= searchResultIndex)
            throw new Exception();

        trackQueue.Enqueue(tracks[searchResultIndex]);
    }

    private readonly record struct CurrentTrack(TrackRequest Track, DateTime StartTime, DateTime? EndTime);

    public void Enqueue(TrackRequest requestTrack)
    {
        trackQueue.Enqueue(requestTrack);
    }
}