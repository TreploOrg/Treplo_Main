namespace Treplo.Models;

public readonly record struct Track(string Title, Thumbnail Thumbnail, StreamInfo Source, string Author, TimeSpan Duration);