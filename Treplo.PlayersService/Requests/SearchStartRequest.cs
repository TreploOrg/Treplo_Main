using Treplo.Common.Models;

namespace Treplo.PlayersService.Requests;

public sealed record SearchStartRequest(ulong SessionId, TrackRequest[] SearchTracks) : PerSessionRequest(SessionId);