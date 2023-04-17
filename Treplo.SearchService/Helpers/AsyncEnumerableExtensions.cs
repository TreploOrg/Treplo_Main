using System.Runtime.CompilerServices;

namespace Treplo.SearchService.Helpers;

public static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> Take<T>(
        this IAsyncEnumerable<T> source,
        uint limit,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var count = 0u;
        if(limit == 0)
            yield break;
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            yield return item;
            count++;
            if (count >= limit || cancellationToken.IsCancellationRequested)
                yield break;
        }
    }
}