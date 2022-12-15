namespace Treplo.PlayersService.Requests;

public sealed record SearchRespondRequest(ulong SessionId, Guid SearchSessionId, uint SearchResultIndex) : PerSessionRequest(SessionId);