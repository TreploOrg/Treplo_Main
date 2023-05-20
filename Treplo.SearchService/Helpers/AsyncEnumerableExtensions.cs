using System.Runtime.CompilerServices;
using SimpleResult;

namespace Treplo.SearchService.Helpers;

public static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<Result<TValue, TError>> TakeSuccessful<TValue, TError>(
        this IAsyncEnumerable<Result<TValue, TError>> source,
        uint limit,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var count = 0u;
        if (limit == 0)
            yield break;
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            yield return item;
            if (item.IsOk)
                count++;
            if (count >= limit || cancellationToken.IsCancellationRequested)
                yield break;
        }
    }
}