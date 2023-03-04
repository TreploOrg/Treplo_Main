using Treplo.Common;

namespace Treplo;

public interface ISessionGrain : IGrainWithStringKey
{
    ValueTask Enqueue(Track track);
    ValueTask StartPlay(ulong voiceChannelId);
    ValueTask<Guid> StartSearch(Track[] searchVariants);
    ValueTask<Track> EndSearch(Guid searchId, uint searchResultIndex);
}