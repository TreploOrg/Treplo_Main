using Treplo.PlayersService.Grains;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Treplo.Common;
using FluentAssertions;
using AutoFixture;

namespace Treplo.PlayersService.Tests;

public class Tests
{
    private readonly ILogger<PlayerGrain> fakeLogger = A.Fake<ILogger<PlayerGrain>>();
    private readonly Fixture fixture = new();
    private PlayerGrain grain = null!;

    [SetUp]
    public void Setup()
    {
        grain = new PlayerGrain(fakeLogger);
    }
    
    [Test]
    public async Task Enqueue_AddOneTrack_ActuallyAddTrack()
    {
        var track = fixture.Create<Track>();
        await grain.Enqueue(track);
        var actual = await grain.GetQueue();
        actual.Should().OnlyContain(x => x == track);
        actual.Should().HaveCount(1);
    }
    
    [Test]
    public async Task Enqueue_AddSeveralTrack_ActuallyAddTrack()
    {
        var tracks = fixture.CreateMany<Track>(5).ToArray();
        foreach (var track in tracks)
            await grain.Enqueue(track);
        
        var actual = await grain.GetQueue();
        actual.Should().Equal(tracks);
    }
    
    [Test]
    public async Task Dequeue_RemoveTrack_ReturnSame()
    {
        var track = fixture.Create<Track>();
        await grain.Enqueue(track);
        var actual = await grain.Dequeue();
        actual.Should().Be(track);
    }
    
    [Test]
    public async Task Dequeue_RemoveSomeTrack_ReturnSame()
    {
        var tracks = fixture.CreateMany<Track>(5).ToArray();
        foreach (var track in tracks)
            await grain.Enqueue(track);

        var actual = new List<Track>();
        while (true)
        {
            var t = await grain.Dequeue();
            if (t == null) break;
            
            actual.Add((Track)t);
        }
        
        actual.Should().Equal(tracks);
    }
    
    [Test]
    public async Task Dequeue_RemoveTrack_ActuallyRemove()
    {
        var track = fixture.Create<Track>();
        await grain.Enqueue(track);
        await grain.Dequeue();
        var actual = await grain.GetQueue();
        actual.Should().HaveCount(0);
    }
    
    [Test]
    public async Task Dequeue_RemoveTrackFromEmpty_ReturnNull()
    {
        var actual = await grain.Dequeue();
        actual.Should().Be(null);
    }
}