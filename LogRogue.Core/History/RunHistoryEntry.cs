using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;

namespace LogRogue.Core.History;

/// <summary>백업이 어떻게 시작됐는지.</summary>
public enum RunTrigger
{
    /// <summary>창에서 실행 버튼을 눌렀다.</summary>
    Manual,

    /// <summary>트레이 메뉴에서 실행했다.</summary>
    Tray,

    /// <summary>예약 시각이 되어 자동으로 실행됐다.</summary>
    Scheduled
}

/// <summary>백업 실행 한 건의 기록. 이력 파일에 한 줄로 저장된다.</summary>
public sealed class RunHistoryEntry
{
    /// <summary>문제 목록에 담을 최대 줄 수. 실패가 많아도 이력 파일이 커지지 않도록 제한한다.</summary>
    public const int MaxProblems = 20;

    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public RunTrigger Trigger { get; set; }

    /// <summary>사람이 읽을 수 있는 기간 설명. 예: 최근 7일 제외 → 2026-09-16 이전 전부</summary>
    public string Period { get; set; } = "";

    public ArchiveGrouping Grouping { get; set; }
    public DeleteMode DeleteMode { get; set; }

    public string SourceRoot { get; set; } = "";
    public string OutputDirectory { get; set; } = "";

    public int ArchivedGroups { get; set; }
    public int FailedGroups { get; set; }
    public int ArchivedDays { get; set; }

    public long OriginalBytes { get; set; }
    public long ArchiveBytes { get; set; }

    public int DeletedDays { get; set; }
    public int FailedDeletions { get; set; }

    /// <summary>실패 사유 목록.</summary>
    public List<string> Problems { get; set; } = new();

    public TimeSpan Duration => FinishedAt - StartedAt;
    public bool HasProblem => FailedGroups > 0 || FailedDeletions > 0;

    /// <summary>실행 결과에서 이력 한 건을 만든다.</summary>
    public static RunHistoryEntry Create(
        BackupPlan plan,
        DeleteMode deleteMode,
        IReadOnlyList<GroupBackupResult> results,
        DateTime startedAt,
        DateTime finishedAt,
        RunTrigger trigger)
    {
        var archived = results.Where(r => r.Archive.Succeeded).ToList();
        var allDeletions = results.SelectMany(r => r.Deletions).ToList();

        var entry = new RunHistoryEntry
        {
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            Trigger = trigger,
            Period = plan.Period.Describe(),
            Grouping = plan.Grouping,
            DeleteMode = deleteMode,
            SourceRoot = plan.SourceRoot,
            OutputDirectory = plan.OutputDirectory,
            ArchivedGroups = archived.Count,
            FailedGroups = results.Count - archived.Count,
            ArchivedDays = archived.Sum(r => r.Archive.Group.Days.Count),
            OriginalBytes = archived.Sum(r => r.Archive.Group.TotalBytes),
            ArchiveBytes = archived.Sum(r => r.Archive.ArchiveBytes),
            DeletedDays = allDeletions.Count(d => d.Result.Succeeded),
            FailedDeletions = allDeletions.Count(d => !d.Result.Succeeded)
        };

        foreach (GroupBackupResult result in results.Where(r => !r.Archive.Succeeded))
            entry.AddProblem($"압축 실패 {result.Archive.Group.FileName}: {result.Archive.Error}");

        foreach (DayDeletion deletion in allDeletions.Where(d => !d.Result.Succeeded))
            entry.AddProblem($"삭제 실패 {deletion.Day.Date:yyyy-MM-dd}: {deletion.Result.Error}");

        return entry;
    }

    private void AddProblem(string text)
    {
        if (Problems.Count < MaxProblems)
            Problems.Add(text);
        else if (Problems.Count == MaxProblems)
            Problems.Add("... 이하 생략");
    }
}
