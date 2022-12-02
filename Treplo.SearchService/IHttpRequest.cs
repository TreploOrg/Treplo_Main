using MediatR;

namespace Treplo.SearchService;

public interface IHttpRequest : IRequest<IResult>
{
}