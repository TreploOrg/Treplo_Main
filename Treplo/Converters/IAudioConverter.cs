using System.IO.Pipelines;

namespace Treplo.Converters;

public interface IAudioConverter
{
    PipeReader Output { get; }
    PipeWriter Input { get; }
    Task Start(CancellationToken cancellationToken = default);
}