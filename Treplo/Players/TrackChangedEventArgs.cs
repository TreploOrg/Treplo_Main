using Treplo.Models;

namespace Treplo.Players;

public readonly record struct TrackChangedEventArgs(IPlayer Sender, Track? OldTrack, Track? NewTrack);