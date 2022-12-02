using Microsoft.AspNetCore.Mvc;
using Treplo.Common.Models;

namespace Treplo.SearchService;

public sealed record SearchRequest(
    [FromQuery(Name = "query")] string Query,
    [FromQuery(Name = "limit")] uint? Limit = null
) : IHttpStreamRequest<TrackSearchResult>;