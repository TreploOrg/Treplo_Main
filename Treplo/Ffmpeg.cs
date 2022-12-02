using System.Text;
using CliWrap;
using Treplo.Common.Models;

namespace Treplo;

public sealed class Ffmpeg : Command
{
    private Ffmpeg(string targetFilePath) : base(targetFilePath)
    {
    }

    private Ffmpeg(
        string targetFilePath,
        string arguments,
        string workingDirPath,
        Credentials credentials,
        IReadOnlyDictionary<string, string?> environmentVariables,
        CommandResultValidation validation,
        PipeSource standardInputPipe,
        PipeTarget standardOutputPipe,
        PipeTarget standardErrorPipe
    )
        : base(targetFilePath, arguments, workingDirPath, credentials, environmentVariables, validation,
            standardInputPipe, standardOutputPipe, standardErrorPipe)
    {
    }

    public static Ffmpeg Create() => new("ffmpeg.exe");

    public Ffmpeg To(Stream outStream, bool autoFlush = false) => new(
        TargetFilePath,
        Arguments,
        WorkingDirPath,
        Credentials,
        EnvironmentVariables,
        Validation,
        StandardInputPipe,
        PipeTarget.ToStream(outStream, autoFlush),
        StandardErrorPipe
    );

    public Ffmpeg ForSource(StreamInfo trackSource) => new(
        TargetFilePath,
        GetArguments(trackSource),
        WorkingDirPath,
        Credentials,
        EnvironmentVariables,
        Validation,
        StandardInputPipe,
        StandardOutputPipe,
        StandardErrorPipe
    );

    public Ffmpeg From(Stream inStream) => new(
        TargetFilePath,
        Arguments,
        WorkingDirPath,
        Credentials,
        EnvironmentVariables,
        Validation,
        PipeSource.FromStream(inStream),
        StandardOutputPipe,
        StandardErrorPipe
    );

    public async ValueTask PipeAsync(CancellationToken cancellationToken = default)
    {
        var errorBuilder = new StringBuilder();
        var command = WithStandardErrorPipe(PipeTarget.ToStringBuilder(errorBuilder));
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