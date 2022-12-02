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
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            if (count >= limit || cancellationToken.IsCancellationRequested)
                yield break;

            yield return item;
            count++;
        }
    }
}