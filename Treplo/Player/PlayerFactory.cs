﻿using Microsoft.Extensions.Logging;
using Treplo.Converters;
using Treplo.Playback;

namespace Treplo.Player;

public interface IPlayerFactory
{
    IPlayer Create(ulong guildId, StreamFormatRequest formatRequest);
}

public class PlayerFactory : IPlayerFactory
{
    private readonly IAudioClient audioClient;
    private readonly IRawAudioSource audioSource;
    private readonly IAudioConverterFactory converterFactory;
    private readonly ILoggerFactory loggerFactory;
    private readonly PlayersService.PlayersService.PlayersServiceClient playersServiceClient;

    public PlayerFactory(
        PlayersService.PlayersService.PlayersServiceClient playersServiceClient,
        IRawAudioSource audioSource,
        IAudioClient audioClient,
        IAudioConverterFactory converterFactory,
        ILoggerFactory loggerFactory
    )
    {
        this.playersServiceClient = playersServiceClient;
        this.audioSource = audioSource;
        this.audioClient = audioClient;
        this.converterFactory = converterFactory;
        this.loggerFactory = loggerFactory;
    }

    public IPlayer Create(ulong guildId, StreamFormatRequest formatRequest) => new Player(
        playersServiceClient,
        loggerFactory.CreateLogger<Player>(),
        audioSource,
        audioClient,
        converterFactory,
        guildId,
        formatRequest
    );
}