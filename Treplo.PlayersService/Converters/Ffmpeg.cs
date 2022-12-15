using System.IO.Pipelines;
using System.Text;
using CliWrap;
using Treplo.Common.Models;

namespace Treplo.PlayersService.Converters;

public sealed class Ffmpeg
{
    private readonly Pipe inputPipe = new();
    private readonly Pipe outputPipe = new();
    private readonly StringBuilder errorBuilder;
    private readonly Command command;

    public Ffmpeg(string path, StreamInfo inStreamInfo, StreamFormatRequest requiredFormat)
    {
        errorBuilder = new StringBuilder();
        command = Cli.Wrap(path)
            .WithValidation(CommandResultValidation.None)
            .WithArguments(GetArguments(inStreamInfo, requiredFormat))
            .WithStandardInputPipe(PipeSource.Create((destination, cancellationToken)
                => inputPipe.Reader.CopyToAsync(destination, cancellationToken)))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(errorBuilder))
            .WithStandardOutputPipe(PipeTarget.Create((source, cancellationToken)
                => source.CopyToAsync(outputPipe.Writer, cancellationToken)));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Exception? localException = null;
        try
        {
            var result = await command.ExecuteAsync(cancellationToken);
            if (result.ExitCode != 0 || errorBuilder.Length > 0)
                throw new FfmpegPipingException(errorBuilder.ToString());
        }
        catch (Exception e)
        {
            localException = e;
            throw;
        }
        finally
        {
            await inputPipe.Reader.CompleteAsync(localException);
            await outputPipe.Writer.CompleteAsync(localException);
        }
    }

    private static string GetArguments(StreamInfo inStreamInfo, StreamFormatRequest requiredFormat)
    {
        var argumentString = new StringBuilder("-hide_banner -loglevel error ");

        var container = inStreamInfo.Container;
        var codec = container.Name == "mp4" ? "aac" : inStreamInfo.Codec.Name;
        argumentString.Append($"-f {container.Name} -codec {codec} ");


        argumentString.Append("-i - ");

        AppendStreamRequest(requiredFormat, argumentString);

        return argumentString.Append('-').ToString();

        static void AppendStreamRequest(StreamFormatRequest streamFormatRequest, StringBuilder stringBuilder)
        {
            if (streamFormatRequest.Channels is { } channels)
                stringBuilder.Append($"-ac {channels} ");
            if (streamFormatRequest.Codec is { } codec)
                stringBuilder.Append($"-c {codec.Name} ");
            if (streamFormatRequest.Container is { } formatContainer)
                stringBuilder.Append($"-f {formatContainer.Name} ");
            if (streamFormatRequest.Frequency is { } frequency)
                stringBuilder.Append($"-ar {frequency} ");
        }
    }

    public PipeReader Output => outputPipe.Reader;
    public PipeWriter Input => inputPipe.Writer;
}

public class FfmpegPipingException : Exception
{
    public FfmpegPipingException(string message) : base(message)
    {
    }
}