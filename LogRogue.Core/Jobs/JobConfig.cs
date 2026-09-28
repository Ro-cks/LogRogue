using System.Text.Json.Serialization;
using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.Scheduling;

namespace LogRogue.Core.Jobs;

/// <summary>
/// 작업 하나의 설정. 대상 폴더 하나를 어떻게 백업할지 전부 담는다.
/// 작업마다 기간, 압축 단위, 삭제 방식, 예약을 따로 가진다.
/// </summary>
public sealed class JobConfig
{
    /// <summary>예전 단일 설정에서 옮겨진 작업의 식별자. 예전 작업 이력과 이어주는 데 쓴다.</summary>
    public const string LegacyId = "legacy";

    /// <summary>작업 식별자. 이름을 바꿔도 이력이 이어지도록 이름과 따로 둔다.</summary>
    public string Id { get; set; } = NewId();

    public string Name { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string OutputPath { get; set; } = "";

    public PeriodMode PeriodMode { get; set; } = PeriodMode.Relative;

    /// <summary>지정한 기간일 때의 시작일.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>지정한 기간일 때의 종료일.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>상대 기간일 때 남겨둘 최근 일수.</summary>
    public int KeepRecentDays { get; set; } = 7;

    public ArchiveGrouping Grouping { get; set; } = ArchiveGrouping.Daily;

    public bool DeleteSource { get; set; }

    /// <summary>삭제 방식. 삭제를 끈 상태에서도 마지막으로 고른 방식을 기억하기 위해 따로 저장한다.</summary>
    public DeleteMode DeleteMode { get; set; } = DeleteMode.RecycleBin;

    public ScheduleSettings Schedule { get; set; } = new();

    /// <summary>실제로 쓸 기간.</summary>
    [JsonIgnore]
    public BackupPeriod Period => PeriodMode == PeriodMode.Relative
        ? BackupPeriod.Relative(KeepRecentDays)
        : BackupPeriod.Absolute(StartDate, EndDate);

    /// <summary>실제로 쓸 삭제 방식. 삭제를 껐으면 None.</summary>
    [JsonIgnore]
    public DeleteMode EffectiveDeleteMode => DeleteSource ? DeleteMode : DeleteMode.None;

    /// <summary>기본값을 채운 새 작업.</summary>
    public static JobConfig CreateNew(string name)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        return new JobConfig
        {
            Name = name,
            StartDate = today.AddDays(-30),
            EndDate = today.AddDays(-1)
        };
    }

    public JobConfig Clone() => new()
    {
        Id = Id,
        Name = Name,
        SourcePath = SourcePath,
        OutputPath = OutputPath,
        PeriodMode = PeriodMode,
        StartDate = StartDate,
        EndDate = EndDate,
        KeepRecentDays = KeepRecentDays,
        Grouping = Grouping,
        DeleteSource = DeleteSource,
        DeleteMode = DeleteMode,
        Schedule = Schedule.Clone()
    };

    public static string NewId() => Guid.NewGuid().ToString("N")[..12];
}
