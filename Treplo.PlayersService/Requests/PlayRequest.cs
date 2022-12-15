using MediatR;
using Treplo.PlayersService.Handlers;

namespace Treplo.PlayersService.Requests;

public sealed record PlayRequest(ulong SessionId) : PerSessionRequest(SessionId);