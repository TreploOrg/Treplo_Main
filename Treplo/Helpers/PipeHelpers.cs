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
            await source.CopyToAsync(output, cancellationToken);
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
}