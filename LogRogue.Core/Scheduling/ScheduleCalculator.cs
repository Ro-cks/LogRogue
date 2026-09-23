namespace LogRogue.Core.Scheduling;

/// <summary>
/// 다음 예약 실행 시각을 계산한다.
/// 시계와 무관한 순수 계산이라 어떤 시각을 넣어도 결과를 확인할 수 있다.
/// </summary>
public static class ScheduleCalculator
{
    /// <summary>예약 시각을 이만큼 지나면 "놓친 실행"으로 본다. 잠깐의 오차는 정상 실행으로 처리한다.</summary>
    public static readonly TimeSpan MissedGrace = TimeSpan.FromMinutes(5);

    /// <summary>
    /// reference 이후 처음 오는 예약 시각. 예약이 꺼져 있거나 요일이 하나도 없으면 null.
    /// </summary>
    public static DateTime? NextAfter(ScheduleSettings schedule, DateTime reference)
    {
        if (!schedule.Enabled)
            return null;

        return schedule.Mode switch
        {
            ScheduleMode.Daily => NextDaily(schedule, reference),
            ScheduleMode.Weekly => NextWeekly(schedule, reference),
            ScheduleMode.Interval => reference.AddHours(Math.Clamp(
                schedule.IntervalHours, ScheduleSettings.MinIntervalHours, ScheduleSettings.MaxIntervalHours)),
            _ => null
        };
    }

    /// <summary>
    /// 지금 기준으로 다음 실행 시각.
    /// 한 번도 실행한 적이 없으면 지금부터 세고, 실행한 적이 있으면 그때부터 센다.
    /// PC가 며칠 꺼져 있었더라도 밀린 횟수만큼 몰아서 실행하지는 않는다. 한 번만 실행된다.
    /// </summary>
    public static DateTime? NextRun(ScheduleSettings schedule, DateTime now)
        => NextAfter(schedule, schedule.LastRunAt ?? now);

    /// <summary>지금 실행할 때가 됐는지.</summary>
    public static bool IsDue(ScheduleSettings schedule, DateTime now)
        => NextRun(schedule, now) is DateTime next && next <= now;

    /// <summary>예약 시각을 한참 지나쳤는지. (PC가 꺼져 있었던 경우)</summary>
    public static bool IsMissed(ScheduleSettings schedule, DateTime now)
        => NextRun(schedule, now) is DateTime next && next <= now - MissedGrace;

    private static DateTime NextDaily(ScheduleSettings schedule, DateTime reference)
    {
        DateTime candidate = reference.Date + schedule.TimeOfDay.ToTimeSpan();
        return candidate > reference ? candidate : candidate.AddDays(1);
    }

    private static DateTime? NextWeekly(ScheduleSettings schedule, DateTime reference)
    {
        if (schedule.Weekdays.Count == 0)
            return null;

        // 오늘부터 이레 뒤까지 훑으면 고른 요일 중 가장 빠른 날이 반드시 나온다
        for (int offset = 0; offset <= 7; offset++)
        {
            DateTime day = reference.Date.AddDays(offset);

            if (!schedule.Weekdays.Contains(day.DayOfWeek))
                continue;

            DateTime candidate = day + schedule.TimeOfDay.ToTimeSpan();
            if (candidate > reference)
                return candidate;
        }

        return null;
    }

    /// <summary>화면에 보여줄 설명. 예: 매주 월, 목 03:00</summary>
    public static string Describe(ScheduleSettings schedule)
    {
        if (!schedule.Enabled)
            return "예약 실행 꺼짐";

        return schedule.Mode switch
        {
            ScheduleMode.Daily => $"매일 {schedule.TimeOfDay:HH:mm}",
            ScheduleMode.Weekly => schedule.Weekdays.Count == 0
                ? "요일이 선택되지 않음"
                : $"매주 {WeekdayNames(schedule.Weekdays)} {schedule.TimeOfDay:HH:mm}",
            ScheduleMode.Interval => $"{schedule.IntervalHours}시간마다",
            _ => "예약 실행 꺼짐"
        };
    }

    /// <summary>월요일부터 순서대로 정렬한 요일 이름. 예: 월, 목</summary>
    public static string WeekdayNames(IEnumerable<DayOfWeek> weekdays)
        => string.Join(", ", weekdays
            .Distinct()
            .OrderBy(d => ((int)d + 6) % 7)
            .Select(ShortName));

    public static string ShortName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "월",
        DayOfWeek.Tuesday => "화",
        DayOfWeek.Wednesday => "수",
        DayOfWeek.Thursday => "목",
        DayOfWeek.Friday => "금",
        DayOfWeek.Saturday => "토",
        _ => "일"
    };
}
