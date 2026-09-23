using LogRogue.Core.Scanning;

namespace LogRogue.Core.Archiving;

/// <summary>압축 파일 하나에 들어갈 날짜 폴더 묶음.</summary>
public sealed class ArchiveGroup
{
    /// <summary>압축 파일 이름(확장자 제외). 예: 2026-08</summary>
    public required string Name { get; init; }

    /// <summary>묶음에 든 날짜 폴더들. 날짜순이며 최소 하나.</summary>
    public required IReadOnlyList<LogDayFolder> Days { get; init; }

    public DateOnly FirstDay => Days[0].Date;
    public DateOnly LastDay => Days[^1].Date;

    public int FileCount => Days.Sum(d => d.FileCount);
    public long TotalBytes => Days.Sum(d => d.TotalBytes);

    public string FileName => Name + ".zip";
}
