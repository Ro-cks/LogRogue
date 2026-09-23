using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;

namespace LogRogue.Core.Settings;

/// <summary>
/// 파일로 저장되는 사용자 설정.
/// 항목을 추가할 때는 기본값을 꼭 지정한다. 예전 설정 파일에는 새 항목이 없으므로 기본값이 쓰인다.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// 설정 파일 형식 버전. 나중에 구조가 크게 바뀌면 이 값을 올리고
    /// 불러올 때 옛 형식을 새 형식으로 변환한다.
    /// </summary>
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>대상 폴더 경로.</summary>
    public string SourcePath { get; set; } = "";

    /// <summary>출력 폴더 경로.</summary>
    public string OutputPath { get; set; } = "";

    /// <summary>원본 삭제 체크 여부.</summary>
    public bool DeleteSource { get; set; }

    /// <summary>
    /// 원본 삭제 방식. 체크를 끈 상태에서도 마지막으로 고른 방식을 기억해두기 위해
    /// DeleteSource와 따로 저장한다.
    /// </summary>
    public DeleteMode DeleteMode { get; set; } = DeleteMode.RecycleBin;

    /// <summary>압축 단위.</summary>
    public ArchiveGrouping Grouping { get; set; } = ArchiveGrouping.Daily;

    /// <summary>기간 지정 방식.</summary>
    public PeriodMode PeriodMode { get; set; } = PeriodMode.Absolute;

    /// <summary>상대 기간일 때 남겨둘 최근 일수.</summary>
    public int KeepRecentDays { get; set; } = 7;
}
