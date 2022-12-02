namespace Treplo.Common.Models;

public readonly record struct StreamInfo(string Url, Codec Codec, Container Container, Bitrate Bitrate, FileSize Size);