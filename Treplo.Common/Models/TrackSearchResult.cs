namespace Treplo.Common.Models;

[GenerateSerializer]
public readonly record struct TrackSearchResult(Track Track, string SearchEngineName);