using Treplo.SearchService.Searching;
using Treplo.SearchService.Searching.Yandex.Music;
using Treplo.SearchService.Searching.Youtube;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;

namespace Treplo.SearchService.Tests;

[TestFixture]
public class RealMixedEnginesTest
{
    private YoutubeEngine youtubeEngine = null!;
    private YandexMusicEngine yandexMusicEngine = null!;
    private ISearchEngineManager manager = null!;
    
    [SetUp]
    public void Setup()
    {
        youtubeEngine = new YoutubeEngine(new HttpFactory());
        yandexMusicEngine = new YandexMusicEngine(new AuthStorage(), new YandexMusicApi());
        manager = new MixedSearchEngineManager(new List<ISearchEngine> {yandexMusicEngine, youtubeEngine});
    }

    //посмотреть как работает
    [Test]
    public void SearchEngines_ShouldWork_Together()
    {
        var searchResult = manager.SearchAsync("AC/DC");
        var results = searchResult.ToBlockingEnumerable().Take(50);
        foreach (var result in results)
        {
            Console.WriteLine( result.IsError? result.UnwrapError() : result.Unwrap().Track.Source.Url);
        }
    }
}