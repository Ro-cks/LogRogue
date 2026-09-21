using LogRogue.Core.Scanning;

namespace LogRogue.Core;

/// <summary>
/// 진행 상황 한 건.
/// Result가 null이면 해당 날짜 처리를 막 시작한 것이고,
/// null이 아니면 그 날짜 처리(압축과 삭제)가 끝난 것이다.
/// </summary>
public sealed record BackupProgress(
    int Index,
    int Total,
    LogDayFolder Folder,
    DayBackupResult? Result);