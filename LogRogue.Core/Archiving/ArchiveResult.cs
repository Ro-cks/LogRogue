using LogRogue.Core.Scanning;

namespace LogRogue.Core.Archiving;

/// <summary>날짜 폴더 하나를 압축한 결과.</summary>
public sealed class ArchiveResult
{
    /// <summary>압축 대상이었던 날짜 폴더.</summary>
    public required LogDayFolder Source { get; init; }

    /// <summary>압축과 검증이 모두 성공했는지 여부.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>생성된 압축 파일 경로. 실패 시 null.</summary>
    public string? ArchivePath { get; init; }

    /// <summary>생성된 압축 파일 크기. 실패 시 0.</summary>
    public long ArchiveBytes { get; init; }

    /// <summary>실패 사유. 성공 시 null.</summary>
    public string? Error { get; init; }

    public static ArchiveResult Success(LogDayFolder source, string archivePath, long archiveBytes) => new()
    {
        Source = source,
        Succeeded = true,
        ArchivePath = archivePath,
        ArchiveBytes = archiveBytes
    };

    public static ArchiveResult Failure(LogDayFolder source, string error) => new()
    {
        Source = source,
        Succeeded = false,
        Error = error
    };
}
