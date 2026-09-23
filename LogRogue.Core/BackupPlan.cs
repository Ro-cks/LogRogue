using LogRogue.Core.Archiving;
using LogRogue.Core.Scanning;

namespace LogRogue.Core;

/// <summary>
/// Prepare 단계에서 확정된 백업 계획. 경로 검증을 이미 통과한 상태다.
/// 확인 창에 보여주고, 그대로 Run에 넘긴다.
/// </summary>
public sealed class BackupPlan
{
    public required string SourceRoot { get; init; }
    public required string OutputDirectory { get; init; }
    public required ArchiveGrouping Grouping { get; init; }
    public required BackupPeriod Period { get; init; }

    /// <summary>기간에 해당하는 날짜 폴더 전체. 날짜순.</summary>
    public required IReadOnlyList<LogDayFolder> Days { get; init; }

    /// <summary>압축 파일 단위로 묶은 결과. 묶음 하나가 압축 파일 하나가 된다.</summary>
    public required IReadOnlyList<ArchiveGroup> Groups { get; init; }
}
