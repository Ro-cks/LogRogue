namespace LogRogue.Core;

/// <summary>기간을 정하는 방식.</summary>
public enum PeriodMode
{
    /// <summary>시작일과 종료일을 직접 고른다.</summary>
    Absolute,

    /// <summary>최근 며칠치만 남기고 그 이전 전부를 대상으로 한다. 실행하는 날에 맞춰 매번 다시 계산된다.</summary>
    Relative
}

/// <summary>
/// 백업할 기간. 실제 날짜 범위는 Resolve로 계산한다.
///
/// 상대 기간은 예약 실행을 위한 것이다.
/// 날짜를 저장해두면 다음 날에는 의미가 없어지지만,
/// "최근 7일은 남긴다"는 규칙은 언제 실행해도 같은 뜻을 유지한다.
/// </summary>
public sealed record BackupPeriod
{
    /// <summary>남길 일수의 허용 범위.</summary>
    public const int MinKeepRecentDays = 1;
    public const int MaxKeepRecentDays = 3650;

    public required PeriodMode Mode { get; init; }

    /// <summary>Absolute일 때 쓰는 시작일.</summary>
    public DateOnly Start { get; init; }

    /// <summary>Absolute일 때 쓰는 종료일.</summary>
    public DateOnly End { get; init; }

    /// <summary>Relative일 때 남겨둘 최근 일수.</summary>
    public int KeepRecentDays { get; init; } = 7;

    public static BackupPeriod Absolute(DateOnly start, DateOnly end)
        => new() { Mode = PeriodMode.Absolute, Start = start, End = end };

    public static BackupPeriod Relative(int keepRecentDays)
        => new() { Mode = PeriodMode.Relative, KeepRecentDays = Clamp(keepRecentDays) };

    /// <summary>
    /// 실제 날짜 범위를 계산한다.
    ///
    /// 상대 기간은 시작일을 두지 않는다. "그 이전 전부"이므로 남아 있는 가장 오래된 폴더까지 모두 대상이다.
    /// 종료일은 오늘에서 남길 일수만큼 뺀 날이다.
    /// 예를 들어 오늘이 9월 23일이고 7일을 남긴다면 9월 17일~23일은 남고 9월 16일 이하가 대상이 된다.
    /// </summary>
    /// <param name="today">오늘로 취급할 날짜. 생략하면 시스템 날짜를 쓴다.</param>
    public (DateOnly Start, DateOnly End) Resolve(DateOnly? today = null)
    {
        if (Mode == PeriodMode.Absolute)
            return Start <= End ? (Start, End) : (End, Start);

        DateOnly reference = today ?? DateOnly.FromDateTime(DateTime.Today);
        return (DateOnly.MinValue, reference.AddDays(-Clamp(KeepRecentDays)));
    }

    /// <summary>화면에 보여줄 설명. 예: 최근 7일 제외 → 2026-09-16 이전 전부</summary>
    public string Describe(DateOnly? today = null)
    {
        (DateOnly start, DateOnly end) = Resolve(today);

        return Mode == PeriodMode.Absolute
            ? $"{start:yyyy-MM-dd} ~ {end:yyyy-MM-dd}"
            : $"최근 {Clamp(KeepRecentDays)}일 제외 → {end:yyyy-MM-dd} 이전 전부";
    }

    public static int Clamp(int keepRecentDays)
        => Math.Clamp(keepRecentDays, MinKeepRecentDays, MaxKeepRecentDays);
}
