namespace LogRogue.Core.Scheduling;

/// <summary>예약 주기 방식.</summary>
public enum ScheduleMode
{
    /// <summary>매일 지정한 시각.</summary>
    Daily,

    /// <summary>지정한 요일마다 지정한 시각.</summary>
    Weekly,

    /// <summary>마지막 실행으로부터 N시간마다.</summary>
    Interval
}

/// <summary>예약 실행 설정. 설정 파일에 함께 저장된다.</summary>
public sealed class ScheduleSettings
{
    public const int MinIntervalHours = 1;
    public const int MaxIntervalHours = 24 * 7;

    /// <summary>예약 실행 사용 여부.</summary>
    public bool Enabled { get; set; }

    public ScheduleMode Mode { get; set; } = ScheduleMode.Daily;

    /// <summary>매일·매주 방식에서 실행할 시각.</summary>
    public TimeOnly TimeOfDay { get; set; } = new(3, 0);

    /// <summary>매주 방식에서 실행할 요일.</summary>
    public List<DayOfWeek> Weekdays { get; set; } = new() { DayOfWeek.Monday };

    /// <summary>시간 간격 방식에서 쓸 간격.</summary>
    public int IntervalHours { get; set; } = 6;

    /// <summary>PC가 꺼져 있어 지나간 예약을 켜진 뒤에 실행할지.</summary>
    public bool CatchUpMissed { get; set; } = true;

    /// <summary>예약 실행 결과를 문제가 있을 때만 알릴지.</summary>
    public bool NotifyOnlyOnProblem { get; set; }

    /// <summary>마지막 예약 실행 시각. 다음 실행 시각 계산의 기준이 된다.</summary>
    public DateTime? LastRunAt { get; set; }

    public ScheduleSettings Clone() => new()
    {
        Enabled = Enabled,
        Mode = Mode,
        TimeOfDay = TimeOfDay,
        Weekdays = new List<DayOfWeek>(Weekdays),
        IntervalHours = IntervalHours,
        CatchUpMissed = CatchUpMissed,
        NotifyOnlyOnProblem = NotifyOnlyOnProblem,
        LastRunAt = LastRunAt
    };
}
