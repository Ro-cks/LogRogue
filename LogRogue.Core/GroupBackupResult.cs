using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.Scanning;

namespace LogRogue.Core;

/// <summary>날짜 폴더 하나의 삭제 결과.</summary>
public sealed record DayDeletion(LogDayFolder Day, DeletionResult Result);

/// <summary>압축 묶음 하나에 대한 압축 결과와, 그 안 날짜들의 원본 삭제 결과.</summary>
public sealed class GroupBackupResult
{
    public required ArchiveResult Archive { get; init; }

    /// <summary>날짜별 삭제 결과. 삭제하지 않았거나 압축에 실패했으면 비어 있다.</summary>
    public IReadOnlyList<DayDeletion> Deletions { get; init; } = Array.Empty<DayDeletion>();

    public int DeletedDays => Deletions.Count(d => d.Result.Succeeded);
    public int FailedDeletions => Deletions.Count(d => !d.Result.Succeeded);
}
