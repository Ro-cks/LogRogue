using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;

namespace LogRogue.Core;

/// <summary>날짜 하나에 대한 압축 결과와 원본 삭제 결과.</summary>
public sealed class DayBackupResult
{
    public required ArchiveResult Archive { get; init; }

    /// <summary>원본 삭제 결과. 삭제하지 않았으면 null.</summary>
    public DeletionResult? Deletion { get; init; }
}
