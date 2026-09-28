using System.Text.Json.Serialization;
using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.Jobs;
using LogRogue.Core.Scheduling;

namespace LogRogue.Core.Settings;

/// <summary>
/// 파일로 저장되는 설정.
/// 항목을 추가할 때는 기본값을 꼭 지정한다. 예전 설정 파일에는 새 항목이 없으므로 기본값이 쓰인다.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// 설정 파일 형식 버전.
    ///   1: 작업이 하나뿐. 경로와 옵션이 맨 위에 바로 있었다.
    ///   2: 작업 여러 개. Jobs 목록 안에 들어간다.
    /// </summary>
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>작업 목록.</summary>
    public List<JobConfig> Jobs { get; set; } = new();

    // ── 버전 1 형식 ──────────────────────────────────────
    // 작업이 하나뿐이던 때의 항목들. 예전 설정 파일을 읽을 때만 쓰고,
    // 불러오는 즉시 작업 하나로 옮긴 뒤 비워서 다시 저장되지 않게 한다.

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SourcePath { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OutputPath { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? DeleteSource { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DeleteMode? DeleteMode { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ArchiveGrouping? Grouping { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public PeriodMode? PeriodMode { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? KeepRecentDays { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ScheduleSettings? Schedule { get; set; }
}
