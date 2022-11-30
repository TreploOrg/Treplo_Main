using System.Text;
using CliWrap;
using Treplo.Models;

namespace Treplo;

public static class Ffmpeg
{
    public static async ValueTask PipeToAsync(Stream inStream, StreamInfo streamInfo, Stream outStream, CancellationToken cancellationToken = default)
    {
        var errorBuilder = new StringBuilder();
        var command = inStream | Cli.Wrap("ffmpeg.exe")
            .WithArguments(GetArguments(streamInfo))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(errorBuilder))
            .WithStandardOutputPipe(PipeTarget.ToStream(outStream, false));

        var result = await command.ExecuteAsync(cancellationToken);

        if (result.ExitCode != 0)
            throw new FfmpegPipingException(errorBuilder.ToString());
    }

    private static string GetArguments(StreamInfo streamInfo)
    {
        var container = streamInfo.Container;
        var codec = container.Name == "mp4" ? "aac" : streamInfo.Codec.Name;

        return $"-hide_banner -loglevel error -f {container.Name} -codec {codec} -i - -ac 2 -f s16le -ar 48000 -";
    }
}

public class FfmpegPipingException : Exception
{
    public FfmpegPipingException(string message) : base(message)
    {
    }
}