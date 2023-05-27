namespace Treplo.Helpers;

public static class GrainHelpers
{
    public static ISessionGrain GetGrain(this IGrainFactory grainFactory, ulong id)
        => grainFactory.GetGrain<ISessionGrain>(unchecked((long)id));

    public static ulong GetGuildId(this ISessionGrain sessionGrain)
        => unchecked((ulong)sessionGrain.GetPrimaryKeyLong());
}