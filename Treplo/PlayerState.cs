using Treplo.Common;
using Treplo.PlayersService;

namespace Treplo;

[GenerateSerializer]
public sealed record PlayerState(LoopState Loop, Track[] Tracks);