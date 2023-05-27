using System.IO.Pipelines;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Treplo.Common;
using Treplo.PlayersService;

namespace Treplo.Playback;

public sealed class RawAudioSource : IRawAudioSource
{
    private readonly ILogger<RawAudioSource> logger;
    private readonly PlayersService.PlayersService.PlayersServiceClient playersServiceClient;

    public RawAudioSource(
        PlayersService.PlayersService.PlayersServiceClient playersServiceClient,
        ILogger<RawAudioSource> logger
    )
    {
        this.playersServiceClient = playersServiceClient;
        this.logger = logger;
    }

    public PipeReader GetAudioPipe(AudioSource source)
    {
        logger.LogDebug("Getting audio pipe");
        var cts = new CancellationTokenSource();
        var result = playersServiceClient.Play(
            new PlayRequest
            {
                AudioSource = source,
            },
            cancellationToken: cts.Token
        );
        logger.LogDebug("Got audio from player");
        return new AudioStreamReaderPipe(result.ResponseStream.ReadAllAsync(cts.Token), result, cts);
    }

    private sealed class AudioStreamReaderPipe : PipeReader
    {
        private readonly IAsyncEnumerable<AudioFrame> audioFramesStream;
        private readonly IDisposable callHandle;
        private readonly Pipe innerPipe;
        private readonly Task pipeTask;
        private CancellationTokenSource? cts;

        public AudioStreamReaderPipe(
            IAsyncEnumerable<AudioFrame> audioFramesStream,
            IDisposable callHandle,
            CancellationTokenSource cts
        )
        {
            this.audioFramesStream = audioFramesStream;
            this.callHandle = callHandle;
            this.cts = cts;
            innerPipe = new Pipe();
            pipeTask = AudioPipeCore(cts.Token);
        }

        private async Task AudioPipeCore(CancellationToken cancellationToken)
        {
            Exception? localException = null;
            var writer = innerPipe.Writer;
            try
            {
                await foreach (var frame in audioFramesStream)
                {
                    await writer.WriteAsync(frame.Bytes.Memory, cancellationToken);
                    if (frame.IsEnd)
                        break;
                }
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
                await writer.CompleteAsync(localException);
                callHandle.Dispose();
                FireCts();
            }
        }

        public override void AdvanceTo(SequencePosition consumed) => innerPipe.Reader.AdvanceTo(consumed);

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
            => innerPipe.Reader.AdvanceTo(consumed, examined);

        public override void CancelPendingRead() => innerPipe.Reader.CancelPendingRead();

        public override void Complete(Exception? exception = null)
        {
            innerPipe.Reader.Complete(exception);
            FireCts();
            callHandle.Dispose();
        }

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
            => innerPipe.Reader.ReadAsync(cancellationToken);

        public override bool TryRead(out ReadResult result) => innerPipe.Reader.TryRead(out result);

        private void FireCts()
        {
            var local = Interlocked.Exchange(ref cts, null);
            local?.Cancel();
            local?.Dispose();
        }
    }
}