using System.IO.Pipelines;
using Discord;
using Discord.Audio;
using Microsoft.Extensions.Logging;
using IDiscordAudioClient = Discord.Audio.IAudioClient;

namespace Treplo.Playback.Discord;

public sealed class DiscordAudioClient : IAudioClient
{
    private readonly IDiscordClient discordClient;
    private readonly ILogger<DiscordAudioClient> logger;

    private ChannelConnection? currentConnection;
    private CurrentAudioConnection? currentAudioConnection;

    public DiscordAudioClient(IDiscordClient discordClient, ILogger<DiscordAudioClient> logger)
    {
        this.discordClient = discordClient;
        this.logger = logger;
    }

    public async Task ConnectToChannel(ulong channelId)
    {
        if (currentConnection is not null && currentConnection.ChannelId != channelId)
            await Disconnect(); // if we are here means client is reconnecting to another channel, so we disconnect from existing one

        var channel = await discordClient.GetChannelAsync(channelId);
        if (channel is not IVoiceChannel voiceChannel)
            throw new InvalidOperationException("Channel is not a voice channel");

        var internalClient = await voiceChannel.ConnectAsync(true);
        internalClient.Disconnected += InternalClientOnDisconnected;

        currentConnection = new ChannelConnection(channelId, internalClient);
    }

    public async ValueTask Disconnect()
    {
        if (currentConnection is null)
            throw new InvalidOperationException("Client is not connected");

        DisconnectAudio();
        await DisconnectFromChannel();
    }


    public async Task ConsumeAudioPipe(PipeReader audioPipe, CancellationToken cancellationToken)
    {
        if (currentConnection is null)
            throw new InvalidOperationException("Client is not connected");

        if (currentAudioConnection is not null)
            throw new InvalidOperationException("Client already playing audio");

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        currentAudioConnection = new CurrentAudioConnection(cts);
        Exception? localException = null;
        try
        {
            await audioPipe.CopyToAsync(currentConnection.Stream, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            localException = e;
        }
        finally
        {
            await audioPipe.CompleteAsync(localException);
            DisconnectAudio();
        }
    }

    public ulong? ChannelId => currentConnection?.ChannelId;

    public async ValueTask DisposeAsync()
    {
        DisconnectAudio();
        await DisconnectFromChannel();
    }

    private async Task InternalClientOnDisconnected(Exception exception)
    {
        DisconnectAudio();

        if (currentConnection is null)
            return; // if we are here means it's a proper disconnect, so no errors

        await DisconnectFromChannel();
        logger.LogError(exception, "Client unexpectedly disconnected");
    }

    private async ValueTask DisconnectFromChannel()
    {
        var local = Interlocked.Exchange(ref currentConnection, null);
        await (local?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    private void DisconnectAudio()
    {
        var local = Interlocked.Exchange(ref currentAudioConnection, null);
        
        local?.Dispose();
    }

    private sealed record ChannelConnection(ulong ChannelId, IDiscordAudioClient CurrentAudioClient) : IAsyncDisposable
    {
        private AudioOutStream? stream;
        public AudioOutStream Stream => stream ??= CurrentAudioClient.CreatePCMStream(AudioApplication.Music);

        public async ValueTask DisposeAsync()
        {
            if (stream is not null)
                await stream.DisposeAsync();
            await CurrentAudioClient.StopAsync();
            CurrentAudioClient.Dispose();
        }
    }

    private sealed record CurrentAudioConnection(CancellationTokenSource CopyCancellation)
        : IDisposable
    {
        public void Dispose()
        {
            CopyCancellation.Cancel();
            CopyCancellation.Dispose();
        }
    }
}