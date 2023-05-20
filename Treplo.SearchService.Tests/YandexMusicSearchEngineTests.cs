using Microsoft.Extensions.Http;
using Treplo.SearchService.Searching.Yandex.Music;
using Treplo.SearchService.Searching.Youtube;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Common;

namespace Treplo.SearchService.Tests;

[TestFixture]
public class YandexMusicSearchEngineTests
{
    private YandexMusicEngine yandexMusicEngine;

    [SetUp]
    public void SetUp()
    {
        yandexMusicEngine = new YandexMusicEngine(new AuthStorage(), new YandexMusicApi());
    }

    [Test]
    public  void Engine_ShouldFindTrack_BySongTitle()
    {
        var collection = yandexMusicEngine.FindInternalAsync("Jailbreak");
        var enumerator = collection.GetAsyncEnumerator();
        enumerator.Current.IsOk.Should().BeTrue();
    }
    
    [Test]
    public void Engine_ShouldMapFoundedTrack_ToGrpc()
    {
        var api = new YandexMusicApi();
        var auth = new AuthStorage();
        var tracks = api.Search.Search(auth, "Killshot", YSearchType.Track).Result.Tracks.Results;
        var resulted =yandexMusicEngine.MapToGrpcTrack(tracks.First()).Result.Unwrap();
        resulted.Title.Should().Be("Killshot");
        resulted.Author.Should().Be("Eminem");
    }
}