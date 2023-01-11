using System.IO.Pipelines;
using MediatR;
using Treplo.PlayersService.Converters;

namespace Treplo.PlayersService.Playback;

public sealed class PlayRequestHandler : IRequestHandler<PlayRequest, IResult>
{
    private readonly FfmpegFactory ffmpegFactory;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<PlayRequestHandler> logger;

    public PlayRequestHandler(
        FfmpegFactory ffmpegFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<PlayRequestHandler> logger
    )
    {
        this.ffmpegFactory = ffmpegFactory;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    public async Task<IResult> Handle(PlayRequest request, CancellationToken cancellationToken)
    {
        var (streamInfo, format) = request;
        var ffmpeg = ffmpegFactory.Create(in streamInfo, in format);
        using var httpClient = httpClientFactory.CreateClient(nameof(PlayRequestHandler));
        var inStream = await httpClient.GetStreamAsync(streamInfo.Url, cancellationToken);

        _ = StartPipe(inStream, ffmpeg, cancellationToken);

        return Results.Stream(ffmpeg.Output);
    }

    //TODO: Need proper way of logging errors
    private async Task StartPipe(Stream inStream, Ffmpeg ffmpeg, CancellationToken cancellationToken)
    {
        Exception? exception = null;
        try
        {
            await using (inStream)
            {
                var inTask = inStream.CopyToAsync(ffmpeg.Input, cancellationToken)
                    .ContinueWith(
                        async (task, input) => await ((PipeWriter)input!).CompleteAsync(task.Exception),
                        ffmpeg.Input,
                        cancellationToken
                    );
                var ffmpegTask = ffmpeg.StartAsync(cancellationToken);

                await Task.WhenAll(inTask, ffmpegTask);
            }
        }
        catch (TaskCanceledException)
        {
            logger.LogDebug("Playback canceled");
        }
        catch (Exception e)
        {
            exception = e;
            logger.LogDebug(e, "Error during playback");
        }
        finally
        {
            await ffmpeg.Output.CompleteAsync(exception);
            logger.LogDebug("Playback finished");
        }
    }
}