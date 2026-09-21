namespace LogRogue.Core.Deletion;

/// <summary>원본 날짜 폴더 하나를 삭제한 결과.</summary>
public sealed class DeletionResult
{
    public required DeleteMode Mode { get; init; }
    public required bool Succeeded { get; init; }

    /// <summary>실패 사유. 성공 시 null.</summary>
    public string? Error { get; init; }

    public static DeletionResult Success(DeleteMode mode) => new() { Mode = mode, Succeeded = true };

    public static DeletionResult Failure(DeleteMode mode, string error) => new()
    {
        Mode = mode,
        Succeeded = false,
        Error = error
    };
}
