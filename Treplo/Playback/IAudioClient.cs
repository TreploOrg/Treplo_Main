using System.IO.Pipelines;

namespace Treplo.Playback;

public interface IAudioClient : IAsyncDisposable
{
    Task ConnectToChannel(ulong channelId);
    ValueTask Disconnect();
    Task ConsumeAudioPipe(PipeReader audioPipe, CancellationToken cancellationToken = default);
    
    ulong? ChannelId { get; }
}