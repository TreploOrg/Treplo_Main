using System.IO.Pipelines;

namespace Treplo.Helpers;

public static class PipeHelpers
{
    public static async Task PipeThrough(
        this PipeReader source,
        PipeWriter destination,
        CancellationToken cancellationToken = default
    )
    {
        Exception? localException = null;
        try
        {
            await source.CopyToAsync(destination, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            localException = e;
            throw;
        }
        finally
        {
            await source.CompleteAsync(localException);
            await destination.CompleteAsync(localException);
        }
    }

    public static async Task PipeThrough(
        this PipeReader source,
        Stream destination,
        CancellationToken cancellationToken
    )
    {
        Exception? localException = null;
        try
        {
            await source.CopyToAsync(destination, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            localException = e;
            throw;
        }
        finally
        {
            await source.CompleteAsync(localException);
        }
    }
}