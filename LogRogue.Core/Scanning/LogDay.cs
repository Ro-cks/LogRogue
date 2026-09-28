namespace LogRogue.Core.Scanning;

/// <summary>수정한 날짜가 같은 파일들의 묶음. 하루치 로그.</summary>
public sealed class LogDay
{
    public required DateOnly Date { get; init; }

    /// <summary>이 날짜에 속하는 파일들. 상대 경로순.</summary>
    public required IReadOnlyList<LogFile> Files { get; init; }

    public int FileCount => Files.Count;
    public long TotalBytes => Files.Sum(f => f.Length);

    public override string ToString()
        => $"{Date:yyyy-MM-dd}  {FileCount,4}개  {ByteSize.ToDisplay(TotalBytes),9}";
}
