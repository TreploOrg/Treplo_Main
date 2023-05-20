using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using SimpleResult;
using Treplo.SearchService.Helpers;
using Treplo.SearchService.Searching;
using Treplo.SearchService.Searching.Errors;

namespace Treplo.SearchService.Tests;

[TestFixture]
public class MixedSearchEngineManagerTests
{
    private ISearchEngine firstEngine = null!;
    private ISearchEngine secondEngine = null!;
    private ISearchEngineManager sut = null!;

    [SetUp]
    public void Setup()
    {
        firstEngine = A.Fake<ISearchEngine>();
        secondEngine = A.Fake<ISearchEngine>();
        sut = new MixedSearchEngineManager(new[] { firstEngine, secondEngine });
    }

    [Test]
    public void Manager_Should_ReturnTracks_AsProduced()
    {
        A.CallTo(() => firstEngine.FindAsync(A<string>._, A<CancellationToken>._))
            .Returns(FastTrack(nameof(firstEngine)));
        A.CallTo(() => secondEngine.FindAsync(A<string>._, A<CancellationToken>._))
            .Returns(SlowTrack(nameof(secondEngine)));

        var result = sut.SearchAsync("");

        var engineNames = result.ToBlockingEnumerable().Select(x => x.UnwrapOrDefault()!.SearchEngineName);

        engineNames.Should().ContainInOrder(nameof(firstEngine), nameof(secondEngine));

        async IAsyncEnumerable<Result<TrackSearchResult, Error>> SlowTrack(string name)
        {
            await Task.Delay(1000);
            yield return new TrackSearchResult
            {
                SearchEngineName = name,
            };
        }

#pragma warning disable CS1998
        async IAsyncEnumerable<Result<TrackSearchResult, Error>> FastTrack(string name)
#pragma warning restore CS1998
        {
            yield return new TrackSearchResult
            {
                SearchEngineName = name,
            };
        }
    }

    [Test]
    public void Manager_Should_StopEnginesWhenSearchEnded()
    {
        A.CallTo(() => firstEngine.FindAsync(A<string>._, A<CancellationToken>._))
            .Returns(SlowTrack(nameof(firstEngine)));
        A.CallTo(() => secondEngine.FindAsync(A<string>._, A<CancellationToken>._))
            .Returns(FastTrack(nameof(secondEngine)));

        var reachedThird = false;

        var result = sut.SearchAsync("");

        var actualResult = result.TakeSuccessful(2).ToBlockingEnumerable().Select(
            (x, i) => x.UnwrapOrDefault()!.SearchEngineName
        ).ToArray();
        
        actualResult.Should().ContainInOrder(nameof(secondEngine), nameof(firstEngine)).And.HaveCount(2);
        reachedThird.Should().BeFalse("enumeration stopped before");


        async IAsyncEnumerable<Result<TrackSearchResult, Error>> SlowTrack(string name)
        {
            await Task.Delay(1000);
            yield return new TrackSearchResult
            {
                SearchEngineName = name,
            };
            await Task.Delay(1000);
            yield return new TrackSearchResult
            {
                SearchEngineName = name,
            };
            await Task.Delay(1000);
            yield return new TrackSearchResult
            {
                SearchEngineName = name,
            };
            reachedThird = true;
        }

#pragma warning disable CS1998
        async IAsyncEnumerable<Result<TrackSearchResult, Error>> FastTrack(string name)
#pragma warning restore CS1998
        {
            yield return new TrackSearchResult
            {
                SearchEngineName = name,
            };
        }
    }
}