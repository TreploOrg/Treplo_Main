using System.Buffers;
using Google.Protobuf;
using Grpc.Core;
using Treplo.PlayersService.Grains;

namespace Treplo.PlayersService;

public sealed class PlayersServiceImpl : PlayersService.PlayersServiceBase
{
    private readonly IClusterClient clusterClient;
    private readonly IHttpClientFactory clientFactory;

    public PlayersServiceImpl(IClusterClient clusterClient, IHttpClientFactory clientFactory)
    {
        this.clusterClient = clusterClient;
        this.clientFactory = clientFactory;
    }

    private static readonly EnqueueResult EnqueueResult = new();

    public override async Task<EnqueueResult> Enqueue(EnqueueRequest request, ServerCallContext context)
    {
        var grain = clusterClient.GetGrain<IPlayerGrain>(request.PlayerRequest.PlayerId);
        await grain.Enqueue(request.Track);
        return EnqueueResult;
    }

    public override async Task<DequeueResult> Dequeue(DequeueRequest request, ServerCallContext context)
    {
        var grain = clusterClient.GetGrain<IPlayerGrain>(request.PlayerRequest.PlayerId);
        return new()
        {
            Track = await grain.Dequeue(),
        };
    }

    public override async Task<LoopResult> Loop(LoopRequest request, ServerCallContext context)
    {
        var grain = clusterClient.GetGrain<IPlayerGrain>(request.PlayerRequest.PlayerId);

        return new()
        {
            Loop = await grain.SwitchLoop(),
        };
    }

    public override async Task<ShuffleResult> Shuffle(ShuffleRequest request, ServerCallContext context)
    {
        var grain = clusterClient.GetGrain<IPlayerGrain>(request.PlayerRequest.PlayerId);

        return new()
        {
            Queue = new()
            {
                Tracks = { await grain.Shuffle() },
            },
        };
    }

    public override async Task<PlayerState> GetState(GetStateRequest request, ServerCallContext context)
    {
        var grain = clusterClient.GetGrain<IPlayerGrain>(request.PlayerRequest.PlayerId);

        return new()
        {
            Loop = await grain.GetLoopState(),
            Queue = new()
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

        var frameSize = request.AudioSource.Bitrate.BitsPerSecond / 8;
        if (frameSize >= int.MaxValue)
            frameSize = int.MaxValue / 2;
        using var buffer = MemoryPool<byte>.Shared.Rent((int)frameSize);
        await using var stream = await response.Content.ReadAsStreamAsync(context.CancellationToken);

        var read = 0;
        while ((read = await stream.ReadAtLeastAsync(
            buffer.Memory,
            (int)frameSize,
            false,
            context.CancellationToken
        )) != 0)
        {
            await responseStream.WriteAsync(
                new()
                {
                    Bytes = UnsafeByteOperations.UnsafeWrap(buffer.Memory[..read]),
                    IsEnd = frameSize > (ulong)read,
                },
                context.CancellationToken
            );
        }
    }
}