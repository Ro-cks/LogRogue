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
    public required IReadOnlyList<LogDayFolder> Targets { get; init; }
}
