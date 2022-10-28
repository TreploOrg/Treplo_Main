using System.Buffers;
using Discord;
using Discord.Audio;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using NAudio.Wave;
using Serilog;
using System;
using Treplo.Models;

namespace Treplo;

public class PlayerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IDateTimeManager dateTimeManager;
    private readonly ISearchClient searchClient;

    private readonly ILogger logger;

    public PlayerModule(ILogger logger, IDateTimeManager dateTimeManager, ISearchClient searchClient)
    {
        this.dateTimeManager = dateTimeManager;
        this.searchClient = searchClient;
        this.logger = logger.ForContext<PlayerModule>();
    }

    [SlashCommand("play", "rickroll's you", true, RunMode.Async)]
    public async Task Play(string query)
    {
        await DeferAsync(ephemeral: true);
        Track? track = null;
        await foreach (var t in searchClient.FindAsync(query))
        {
            track = t;
            break;
        }

        if (track is null)
            await FollowupAsync($"Couldn't find song {query}", ephemeral: true);
        var voiceChannel = (Context.User as IGuildUser)!.VoiceChannel;
        var sourceStreamFile = track.Value.Source.Url;
        using var audioClient = await voiceChannel.ConnectAsync();
        await FollowupAsync($"{query} in progress", ephemeral: true);
        await StartPlayBack(audioClient, sourceStreamFile);
        await ModifyOriginalResponseAsync(x => x.Content = $"{query} finished");
    }

    private readonly WaveFormat outputFormat = new(48000, 16, 2);

    private async Task StartPlayBack(IAudioClient audioClient, string sourceStreamPath)
    {
        await using var inAudio = new MediaFoundationReader(sourceStreamPath);
        await using var resampler = new WaveFormatConversionStream(outputFormat, inAudio);
        var blocksSize = outputFormat.AverageBytesPerSecond / 50;
        await StreamCopy(audioClient, resampler, blocksSize);
    }

    private async Task StreamCopy(IAudioClient audioClient, Stream source, int bufferSize)
    {
        await using var destinationStream = audioClient.CreatePCMStream(AudioApplication.Music);
        try
        {
            await source.CopyToAsync(destinationStream);
        }
        catch (OperationCanceledException)
        {
            logger.Information("Playback was canceled");
        }
        finally
        {
            var timeoutTask = Task.Delay(1000);
            var flushTask = destinationStream.FlushAsync();

            await Task.WhenAny(timeoutTask, flushTask);
        }
    }
}