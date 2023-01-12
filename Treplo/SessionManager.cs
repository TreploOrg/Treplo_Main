using System.Collections.Concurrent;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Treplo.Clients;

namespace Treplo;

//TODO: Orleans would be more reliable way to handle this then we'll need replication
public sealed class SessionManager : IAsyncDisposable
{
    private readonly IClusterClient clusterClient;
    private readonly DiscordSocketClient discordSocketClient;
    private readonly PlayerServiceClient playerServiceClient;
    private readonly ILoggerFactory loggerFactory;
    private readonly ConcurrentDictionary<ulong, Session> sessions = new();

    public SessionManager(
        IClusterClient clusterClient,
        DiscordSocketClient discordSocketClient,
        PlayerServiceClient playerServiceClient,
        ILoggerFactory loggerFactory
    )
    {
        this.clusterClient = clusterClient;
        this.discordSocketClient = discordSocketClient;
        this.playerServiceClient = playerServiceClient;
        this.loggerFactory = loggerFactory;
    }

    public Session GetSession(ulong sessionId)
    {
        return sessions.GetOrAdd(sessionId,
            static (id, args) =>
                new Session(
                    id,
                    args.clusterClient,
                    args.discordSocketClient,
                    args.playerServiceClient,
                    args.logger
                ),
            (clusterClient, discordSocketClient, playerServiceClient, logger: loggerFactory.CreateLogger<Session>())
        );
    }
    
    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(sessions.Select(x => x.Value.DisposeAsync().AsTask()));
    }
}