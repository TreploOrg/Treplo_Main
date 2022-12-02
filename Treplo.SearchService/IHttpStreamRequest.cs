using MediatR;

namespace Treplo.SearchService;

public interface IHttpStreamRequest<out TItem> : IStreamRequest<TItem>
{
}