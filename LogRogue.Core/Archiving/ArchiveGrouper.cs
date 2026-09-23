using LogRogue.Core.Scanning;

namespace LogRogue.Core.Archiving;

/// <summary>날짜 폴더 목록을 압축 단위에 맞춰 묶는다.</summary>
public static class ArchiveGrouper
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <param name="days">날짜순으로 정렬된 날짜 폴더 목록.</param>
    public static IReadOnlyList<ArchiveGroup> Group(IReadOnlyList<LogDayFolder> days, ArchiveGrouping grouping)
    {
        if (days.Count == 0)
            return Array.Empty<ArchiveGroup>();

        if (grouping == ArchiveGrouping.WholeRange)
            return new[] { new ArchiveGroup { Name = RangeName(days[0].Date, days[^1].Date), Days = days } };

        // 같은 이름이 나오는 날짜끼리 묶는다. days가 날짜순이므로 묶음 순서도 날짜순이 된다.
        return days
            .GroupBy(day => NameFor(day.Date, grouping))
            .Select(g => new ArchiveGroup { Name = g.Key, Days = g.ToList() })
            .ToList();
    }

    /// <summary>
    /// 날짜 하나가 들어갈 압축 파일 이름.
    /// 주별·월별은 실제로 로그가 있는 날이 아니라 달력 기준으로 정한다.
    /// 그래야 나중에 같은 주·같은 달의 다른 날을 압축할 때 같은 파일에 합쳐진다.
    /// </summary>
    public static string NameFor(DateOnly date, ArchiveGrouping grouping) => grouping switch
    {
        ArchiveGrouping.Daily => date.ToString(DateFormat),
        ArchiveGrouping.Weekly => RangeName(StartOfWeek(date), StartOfWeek(date).AddDays(6)),
        ArchiveGrouping.Monthly => date.ToString("yyyy-MM"),
        _ => throw new ArgumentOutOfRangeException(nameof(grouping), grouping, "기간 전체는 날짜 하나로 이름을 정할 수 없습니다.")
    };

    /// <summary>그 주의 월요일.</summary>
    public static DateOnly StartOfWeek(DateOnly date)
    {
        // DayOfWeek는 일요일=0, 월요일=1 ... 토요일=6
        int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    private static string RangeName(DateOnly first, DateOnly last)
        => first == last
            ? first.ToString(DateFormat)
            : $"{first.ToString(DateFormat)}_{last.ToString(DateFormat)}";
}
