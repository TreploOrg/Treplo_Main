using Treplo.Common.Models;

namespace Treplo.PlayersService.Requests;

public record EnqueueRequest(ulong SessionId, TrackRequest Track) : PerSessionRequest(SessionId);