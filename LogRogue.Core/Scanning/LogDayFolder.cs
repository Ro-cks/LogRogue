namespace LogRogue.Core.Scanning;

/// <summary>
/// 하루치 로그가 담긴 날짜 폴더 하나를 나타낸다.
/// 예: C:\Logs\2026-08-20
/// </summary>
public sealed class LogDayFolder
{
    /// <summary>폴더 이름에서 파싱한 날짜.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>폴더의 전체 경로.</summary>
    public required string Path { get; init; }

    /// <summary>하위 폴더까지 포함한 파일 개수.</summary>
    public required int FileCount { get; init; }

    /// <summary>하위 폴더까지 포함한 총 바이트 수.</summary>
    public required long TotalBytes { get; init; }

    public override string ToString()
        => $"{Date:yyyy-MM-dd}  {FileCount,4}개  {ByteSize.ToDisplay(TotalBytes),9}";
}
