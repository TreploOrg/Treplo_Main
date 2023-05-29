using System.Buffers;
using System.Threading.Channels;
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
            await ReadFromUrl(responseStream, context, frameSize, request);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static readonly UnboundedChannelOptions ChannelOptions = new() { SingleWriter = true, SingleReader = true };

    private async Task ReadFromUrl(
        IServerStreamWriter<AudioFrame> responseStream,
        ServerCallContext context,
        ulong frameSize,
        PlayRequest request
    )
    {
        var cachePipe = Channel.CreateUnbounded<AudioFrame>(ChannelOptions);
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

        var copyToCacheTask = CopyToCacheTask(stream, (int)frameSize, cachePipe.Writer, context.CancellationToken);
        logger.LogDebug("Starting wait");
        var outputTask = StartOutputStream(
            responseStream,
            cachePipe.Reader,
            context.CancellationToken
        );

        await Task.WhenAll(copyToCacheTask, outputTask);
    }

    private async Task CopyToCacheTask(
        Stream stream,
        int frameSize,
        ChannelWriter<AudioFrame> writer,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using var memoryOwner = MemoryPool<byte>.Shared.Rent(frameSize);
            logger.LogDebug("Started copying data");
            while (!cancellationToken.IsCancellationRequested)
            {
                var readResult = await stream.ReadAtLeastAsync(memoryOwner.Memory, frameSize, false, cancellationToken);
                var isEnd = readResult < frameSize;
                await writer.WriteAsync(
                    new AudioFrame
                    {
                        Bytes = ByteString.CopyFrom(memoryOwner.Memory[..readResult].Span),
                        IsEnd = isEnd,
                    },
                    cancellationToken
                );
                
                if(isEnd)
                    return;
            }
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
            writer.Complete();
        }
    }

    private async Task StartOutputStream(
        IServerStreamWriter<AudioFrame> serverStreamWriter,
        ChannelReader<AudioFrame> reader,
        CancellationToken cancellationToken
    )
    {
        logger.LogDebug("Started output");
        try
        {
            while (!cancellationToken.IsCancellationRequested && await reader.WaitToReadAsync(cancellationToken))
            {
                while (reader.TryRead(out var frame))
                    await serverStreamWriter.WriteAsync(frame, cancellationToken);
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