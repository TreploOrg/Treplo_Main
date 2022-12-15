using MediatR;

namespace Treplo.PlayersService.Requests;

public record PerSessionRequest(ulong SessionId) : IRequest<IResult>;