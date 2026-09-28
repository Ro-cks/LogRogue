namespace LogRogue.Core.Scanning;

/// <summary>백업 대상 파일 하나.</summary>
/// <param name="FullPath">파일의 전체 경로.</param>
/// <param name="RelativePath">대상 폴더 기준 상대 경로. 구분자는 /. 압축 파일 안의 경로로 그대로 쓴다.</param>
/// <param name="Length">스캔 시점의 크기.</param>
/// <param name="LastWriteTime">스캔 시점의 수정한 날짜(현지 시각).</param>
public sealed record LogFile(string FullPath, string RelativePath, long Length, DateTime LastWriteTime)
{
    /// <summary>이 파일이 속하는 날짜. 수정한 날짜 기준.</summary>
    public DateOnly Date => DateOnly.FromDateTime(LastWriteTime);
}
