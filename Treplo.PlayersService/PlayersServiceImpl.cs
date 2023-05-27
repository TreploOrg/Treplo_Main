using System.Buffers;
using Google.Protobuf;
using Grpc.Core;
using Treplo.PlayersService.Grains;

namespace Treplo.PlayersService;

public sealed class PlayersServiceImpl : PlayersService.PlayersServiceBase
{
    private static readonly EnqueueResult EnqueueResult = new();


    private static readonly string musicCache = $"{AppContext.BaseDirectory}/music";
    private readonly IHttpClientFactory clientFactory;
    private readonly IClusterClient clusterClient;
    private readonly ILogger<PlayersServiceImpl> logger;

    static PlayersServiceImpl()
    {
        Directory.CreateDirectory(musicCache);
    }

    public PlayersServiceImpl(
        IClusterClient clusterClient,
        IHttpClientFactory clientFactory,
        ILogger<PlayersServiceImpl> logger
    )
    {
        this.clusterClient = clusterClient;
        this.clientFactory = clientFactory;
        this.logger = logger;
    }

    public override async Task<EnqueueResult> Enqueue(EnqueueRequest request, ServerCallContext context)
    {
        var grain = clusterClient.GetGrain<IPlayerGrain>(request.PlayerRequest.PlayerId);
        await grain.Enqueue(request.Track);
        return EnqueueResult;
    }

    public override async Task<DequeueResult> Dequeue(DequeueRequest request, ServerCallContext context)
    {
        var grain = clusterClient.GetGrain<IPlayerGrain>(request.PlayerRequest.PlayerId);
        return new DequeueResult
        {
            Track = await grain.Dequeue(),
        };
    }

    public override async Task<LoopResult> Loop(LoopRequest request, ServerCallContext context)
    {
        var grain = clusterClient.GetGrain<IPlayerGrain>(request.PlayerRequest.PlayerId);

        return new LoopResult
        {
            Loop = await grain.SwitchLoop(),
        };
    }

    public override async Task<ShuffleResult> Shuffle(ShuffleRequest request, ServerCallContext context)
    {
        var grain = clusterClient.GetGrain<IPlayerGrain>(request.PlayerRequest.PlayerId);

        return new ShuffleResult
        {
            Queue = new Queue
            {
                Tracks = { await grain.Shuffle() },
            },
        };
    }

    public override async Task<PlayerState> GetState(GetStateRequest request, ServerCallContext context)
    {
        var grain = clusterClient.GetGrain<IPlayerGrain>(request.PlayerRequest.PlayerId);

        return new PlayerState
        {
            Loop = await grain.GetLoopState(),
            Queue = new Queue
            {
                Tracks = { await grain.GetQueue() },
            },
        };
    }

    public override async Task Play(
        PlayRequest request,
        IServerStreamWriter<AudioFrame> responseStream,
        ServerCallContext context
    )
    {
        var frameSize = request.AudioSource.Bitrate.BitsPerSecond / 8;
        if (frameSize >= int.MaxValue)
            frameSize = int.MaxValue / 2;

        try
        {
            var cachePath = Path.GetTempFileName();

            await ReadFromUrl(responseStream, context, cachePath, frameSize, request);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ReadFromUrl(
        IServerStreamWriter<AudioFrame> responseStream,
        ServerCallContext context,
        string cachePath,
        ulong frameSize,
        PlayRequest request
    )
    {
        await using var cachePipe = new FilePipe(cachePath);
        using var client = clientFactory.CreateClient(nameof(PlayersServiceImpl));
        using var message = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(request.AudioSource.Url),
        };
        using var response = await client.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            context.CancellationToken
        );

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(context.CancellationToken);

        var copyToCacheTask = CopyToCacheTask(stream, cachePipe.Writer, context.CancellationToken);
        logger.LogDebug("Starting wait");
        await Task.Delay(500, context.CancellationToken);
        var outputTask = StartOutputStream(
            responseStream,
            (int)frameSize,
            cachePipe.Reader,
            static pipe => pipe.ShouldTryRead,
            cachePipe,
            context.CancellationToken
        );

        await Task.WhenAll(copyToCacheTask, outputTask);
    }

    private async Task CopyToCacheTask(
        Stream stream,
        FilePipe.NotifyingWrapper writer,
        CancellationToken cancellationToken
    )
    {
        await using (writer)
        {
            try
            {
                logger.LogDebug("Started copying");
                await stream.CopyToAsync(writer.Stream);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error during audio receiving from source");
            }
            finally
            {
                logger.LogDebug("Finished downloading audio");
            }
        }
    }

    private async Task StartOutputStream<T>(
        IServerStreamWriter<AudioFrame> serverStreamWriter,
        int frameSize,
        Stream reader,
        Func<T, bool> shouldTryReadNext,
        T data,
        CancellationToken cancellationToken
    )
    {
        logger.LogDebug("Started output");
        using var buffer = MemoryPool<byte>.Shared.Rent(frameSize);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var readResult = await reader.ReadAtLeastAsync(buffer.Memory, frameSize, false, cancellationToken);
                var isEnd = readResult < frameSize;
                if (readResult != 0)
                {
                    logger.LogError("Got buffer");
                    await serverStreamWriter.WriteAsync(
                        new AudioFrame
                        {
                            Bytes = UnsafeByteOperations.UnsafeWrap(buffer.Memory[..readResult]),
                            IsEnd = isEnd && !shouldTryReadNext(data),
                        },
                        cancellationToken
                    );
                }

                if (isEnd && shouldTryReadNext(data))
                    await Task.Delay(100, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during sending audio to client");
        }
        finally
        {
            logger.LogDebug("Sent all audio");
        }
    }
}

internal class FilePipe : IAsyncDisposable
{
    private readonly string path;
    private int shouldTryRead = 1;

    public FilePipe(string path)
    {
        this.path = path;
        Writer = new NotifyingWrapper(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), this);
        Reader = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    }

    public Stream Reader { get; }
    public NotifyingWrapper Writer { get; }

    public bool ShouldTryRead => shouldTryRead == 1;

    public async ValueTask DisposeAsync()
    {
        await Writer.DisposeAsync();
        await Reader.DisposeAsync();
        File.Delete(path);
    }

    private void CompleteWriter()
    {
        Interlocked.Exchange(ref shouldTryRead, 0);
    }

    public class NotifyingWrapper : IAsyncDisposable
    {
        private readonly FilePipe pipe;

        public NotifyingWrapper(Stream stream, FilePipe pipe)
        {
            this.pipe = pipe;
            Stream = stream;
        }

        public Stream Stream { get; }

        public async ValueTask DisposeAsync()
        {
            await Stream.DisposeAsync();
            pipe.CompleteWriter();
        }
    }
}