namespace LogRogue.Core.Archiving;

/// <summary>날짜 폴더들을 압축 파일 하나에 어떻게 묶을지.</summary>
public enum ArchiveGrouping
{
    /// <summary>하루에 압축 파일 하나. 예: 2026-08-20.zip</summary>
    Daily,

    /// <summary>월요일~일요일 한 주에 하나. 예: 2026-08-17_2026-08-23.zip</summary>
    Weekly,

    /// <summary>한 달에 하나. 예: 2026-08.zip</summary>
    Monthly,

    /// <summary>선택한 기간 전체를 하나로. 예: 2026-08-17_2026-09-09.zip</summary>
    WholeRange
}
