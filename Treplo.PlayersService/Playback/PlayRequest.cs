using MediatR;
using Treplo.Common.Models;

namespace Treplo.PlayersService.Playback;

public sealed record PlayRequest(StreamInfo StreamInfo, StreamFormatRequest FormatRequest) : IRequest<IResult>;