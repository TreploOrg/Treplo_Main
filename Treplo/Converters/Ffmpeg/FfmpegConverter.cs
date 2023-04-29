using System.IO.Pipelines;
using System.Text;
using CliWrap;
using Treplo.Common;

namespace Treplo.Converters.Ffmpeg;

public sealed class FfmpegConverter : IAudioConverter
{
    private readonly Command command;
    private readonly StringBuilder errorBuilder;
    private readonly Pipe inputPipe = new();
    private readonly Pipe outputPipe = new();

    public FfmpegConverter(string path, AudioSource audioSource, in StreamFormatRequest requiredFormat)
    {
        errorBuilder = new StringBuilder();
        command = Cli.Wrap(path)
            .WithValidation(CommandResultValidation.None)
            .WithArguments(GetArguments(audioSource, in requiredFormat))
            .WithStandardInputPipe(
                PipeSource.Create(
                    (destination, cancellationToken)
                        => inputPipe.Reader.CopyToAsync(destination, cancellationToken)
                )
            )
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(errorBuilder))
            .WithStandardOutputPipe(
                PipeTarget.Create(
                    (source, cancellationToken)
                        => source.CopyToAsync(outputPipe.Writer, cancellationToken)
                )
            );
    }

    public async Task Start(CancellationToken cancellationToken)
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

    public PipeReader Output => outputPipe.Reader;
    public PipeWriter Input => inputPipe.Writer;

    private static string GetArguments(AudioSource audioSource, in StreamFormatRequest requiredFormat)
    {
        var argumentString = new StringBuilder("-hide_banner -loglevel error ");

        var container = audioSource.Container;
        var codec = container.Name == "mp4" ? "aac" : audioSource.Codec.Name;
        argumentString.Append($"-f {container.Name} -codec {codec} ");


        argumentString.Append("-i - ");

        AppendStreamRequest(in requiredFormat, argumentString);

        return argumentString.Append('-').ToString();

        static void AppendStreamRequest(in StreamFormatRequest streamFormatRequest, StringBuilder stringBuilder)
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
}

public class FfmpegPipingException : Exception
{
    public FfmpegPipingException(string message) : base(message)
    {
    }
}