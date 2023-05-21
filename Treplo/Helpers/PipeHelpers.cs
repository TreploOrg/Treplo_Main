using System.Buffers;
using System.IO.Pipelines;

namespace Treplo.Helpers;

public static class PipeHelpers
{
    public static async Task PipeThrough(
        this PipeReader source,
        PipeWriter output,
        CancellationToken cancellationToken = default
    )
    {
        Exception? localException = null;
        try
        {
            await CopyToAsync(
                source,
                output,
                static (destination, memory, cancellationToken) => destination.WriteAsync(memory, cancellationToken),
                cancellationToken
            );
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
            await output.CompleteAsync(localException);
        }
    }

    private static async Task CopyToAsync<TStream>(
        PipeReader source,
        TStream destination,
        Func<TStream, ReadOnlyMemory<byte>, CancellationToken, ValueTask<FlushResult>> writeAsync,
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            ReadResult result = await source.ReadAsync(cancellationToken).ConfigureAwait(false);
            ReadOnlySequence<byte> buffer = result.Buffer;
            SequencePosition position = buffer.Start;
            SequencePosition consumed = position;

            try
            {
                if (result.IsCanceled)
                {
                    throw new Exception();
                }

                while (buffer.TryGet(ref position, out ReadOnlyMemory<byte> memory))
                {
                    Console.WriteLine("writing buffer to pipe");
                    FlushResult flushResult =
                        await writeAsync(destination, memory, cancellationToken).ConfigureAwait(false);

                    if (flushResult.IsCanceled)
                    {
                        throw new Exception();
                    }

                    consumed = position;

                    if (flushResult.IsCompleted)
                    {
                        return;
                    }
                }

                // The while loop completed successfully, so we've consumed the entire buffer.
                consumed = buffer.End;

                if (result.IsCompleted)
                {
                    break;
                }
            }
            finally
            {
                // Advance even if WriteAsync throws so the PipeReader is not left in the
                // currently reading state
                source.AdvanceTo(consumed);
            }
        }
    }
}