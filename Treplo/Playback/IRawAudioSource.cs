using System.IO.Pipelines;
using Treplo.Common;

namespace Treplo.Playback;

public interface IRawAudioSource
{
    PipeReader GetAudioPipe(AudioSource source);
}