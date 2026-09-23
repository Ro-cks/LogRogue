namespace LogRogue.Core.Archiving;

/// <summary>묶음 하나를 압축한 결과.</summary>
public sealed class ArchiveResult
{
    /// <summary>압축 대상이었던 묶음.</summary>
    public required ArchiveGroup Group { get; init; }

    /// <summary>압축과 검증이 모두 성공했는지 여부.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>생성된 압축 파일 경로. 실패 시 null.</summary>
    public string? ArchivePath { get; init; }

    /// <summary>생성된 압축 파일 크기. 실패 시 0.</summary>
    public long ArchiveBytes { get; init; }

    /// <summary>같은 이름의 기존 압축 파일에서 옮겨 담은 항목 수. 새로 만들었으면 0.</summary>
    public int CarriedOverEntries { get; init; }

    /// <summary>기존 압축 파일을 읽지 못해 번호를 붙여 따로 저장했는지.</summary>
    public bool SavedSeparately { get; init; }

    /// <summary>실패 사유. 성공 시 null.</summary>
    public string? Error { get; init; }

    public static ArchiveResult Failure(ArchiveGroup group, string error) => new()
    {
        Group = group,
        Succeeded = false,
        Error = error
    };
}
