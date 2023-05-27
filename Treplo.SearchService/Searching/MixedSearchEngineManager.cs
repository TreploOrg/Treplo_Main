using System.Threading.Channels;
using SimpleResult;
using Treplo.SearchService.Searching.Errors;

namespace Treplo.SearchService.Searching;

public class MixedSearchEngineManager : ISearchEngineManager
{
    private readonly ISearchEngine[] engines;

    public MixedSearchEngineManager(IEnumerable<ISearchEngine> engines)
    {
        this.engines = engines.ToArray();
    }

    public IAsyncEnumerable<Result<TrackSearchResult, Error>> SearchAsync(
        string searchQuery,
        CancellationToken cancellationToken = default
    ) => new Enumerable(searchQuery, engines, cancellationToken);

    private class Enumerable : IAsyncEnumerable<Result<TrackSearchResult, Error>>
    {
        private readonly ICollection<ISearchEngine> engines;
        private readonly string query;
        private readonly CancellationToken searchToken;

        public Enumerable(string query, ICollection<ISearchEngine> engines, CancellationToken searchToken)
        {
            this.query = query;
            this.engines = engines;
            this.searchToken = searchToken;
        }

        public IAsyncEnumerator<Result<TrackSearchResult, Error>> GetAsyncEnumerator(
            CancellationToken cancellationToken
        )
        {
            var channel = Channel.CreateBounded<Result<TrackSearchResult, Error>>(
                new BoundedChannelOptions(engines.Count)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleWriter = false,
                    SingleReader = true,
                }
            );

            var cs = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, searchToken);
            var searchTask = StartSearch(channel.Writer, cs.Token);
            return new Enumerator(query, channel.Reader, cs, searchTask);
        }

        private async Task StartSearch(
            ChannelWriter<Result<TrackSearchResult, Error>> output,
            CancellationToken cancellationToken
        )
        {
            Exception? localException = null;
            try
            {
                await Task.WhenAll(
                    engines.Select(engine => StartEngineSearch(engine, query, output, cancellationToken))
                );
            }
            catch (Exception e)
            {
                localException = e;
            }
            finally
            {
                // one of the engines threw, should not happen but we'll propagate to the channel just in case
                output.Complete(localException);
            }
        }

        private static async Task StartEngineSearch(
            ISearchEngine engine,
            string query,
            ChannelWriter<Result<TrackSearchResult, Error>> outputSink,
            CancellationToken cancellationToken
        )
        {
            await foreach (var result in engine.FindAsync(query, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                try
                {
                    await outputSink.WriteAsync(result, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private class Enumerator : IAsyncEnumerator<Result<TrackSearchResult, Error>>
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly string query;
        private readonly Task searchTask;
        private readonly ChannelReader<Result<TrackSearchResult, Error>> tracksReader;
        private Result<TrackSearchResult, Error> current;

        private bool readAvailable;
        private bool threw;

        public Enumerator(
            string query,
            ChannelReader<Result<TrackSearchResult, Error>> tracksReader,
            CancellationTokenSource cancellationTokenSource,
            Task searchTask
        )
        {
            this.query = query;
            this.tracksReader = tracksReader;
            this.cancellationTokenSource = cancellationTokenSource;
            this.searchTask = searchTask;
        }

        public Result<TrackSearchResult, Error> Current => current;

        public async ValueTask<bool> MoveNextAsync()
        {
            while (true)
            {
                if (cancellationTokenSource.IsCancellationRequested)
                    return false;
                if (!readAvailable && !threw)
                    try
                    {
                        readAvailable = await tracksReader.WaitToReadAsync(cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        readAvailable = false;
                    }
                    catch (Exception e)
                    {
                        current = Error.ErrorInSearch(query, e);
                        threw = true;
                        return true;
                    }

                if (!readAvailable || threw) return false;

                readAvailable = tracksReader.TryRead(out current);
                if (readAvailable)
                    break;
            }

            return true;
        }

        public async ValueTask DisposeAsync()
        {
            cancellationTokenSource.Cancel();
            await searchTask;
            cancellationTokenSource.Dispose();
        }
    }
}