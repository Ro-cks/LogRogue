using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.Scanning;

namespace LogRogue.Core;

/// <summary>하루치 원본 파일의 삭제 결과.</summary>
public sealed record DayDeletion(LogDay Day, DeletionResult Result);

/// <summary>압축 묶음 하나에 대한 압축 결과와, 그 안 날짜들의 원본 삭제 결과.</summary>
public sealed class GroupBackupResult
{
    public required ArchiveResult Archive { get; init; }

    /// <summary>날짜별 삭제 결과. 삭제하지 않았거나 압축에 실패했으면 비어 있다.</summary>
    public IReadOnlyList<DayDeletion> Deletions { get; init; } = Array.Empty<DayDeletion>();

    /// <summary>원본을 남김없이 정리한 날짜 수.</summary>
    public int DeletedDays => Deletions.Count(d => d.Result.Succeeded);

    /// <summary>일부라도 지우지 못한 파일이 남은 날짜 수.</summary>
    public int FailedDeletions => Deletions.Count(d => !d.Result.Succeeded);

    public int DeletedFiles => Deletions.Sum(d => d.Result.DeletedFiles);
    public long DeletedBytes => Deletions.Sum(d => d.Result.DeletedBytes);
}
