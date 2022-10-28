namespace Treplo;

public interface IDateTimeManager
{
    DateTime UtcNow { get; }
    DateTimeOffset UtcOffsetNow { get; }
}

public class DateTimeManager : IDateTimeManager
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTimeOffset UtcOffsetNow => DateTimeOffset.UtcNow;
}