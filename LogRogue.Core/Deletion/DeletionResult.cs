namespace LogRogue.Core.Deletion;

/// <summary>하루치 원본 파일들을 삭제한 결과.</summary>
public sealed class DeletionResult
{
    public required DeleteMode Mode { get; init; }

    /// <summary>실제로 지운 파일 수.</summary>
    public int DeletedFiles { get; init; }

    /// <summary>실제로 지운 파일들의 총 크기.</summary>
    public long DeletedBytes { get; init; }

    /// <summary>지우지 못했거나 일부러 남긴 파일과 그 사유.</summary>
    public IReadOnlyList<string> Problems { get; init; } = Array.Empty<string>();

    /// <summary>문제없이 끝났는지. 남은 파일이 하나라도 있으면 false.</summary>
    public bool Succeeded => Problems.Count == 0;

    /// <summary>대표 사유 한 줄. 여러 건이면 첫 번째와 나머지 개수.</summary>
    public string? Error => Problems.Count switch
    {
        0 => null,
        1 => Problems[0],
        _ => $"{Problems[0]} 외 {Problems.Count - 1}건"
    };

    public static DeletionResult Failure(DeleteMode mode, string error) => new()
    {
        Mode = mode,
        Problems = new[] { error }
    };
}
