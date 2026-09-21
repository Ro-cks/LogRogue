namespace LogRogue.Core.Deletion;

/// <summary>압축에 성공한 원본 날짜 폴더를 어떻게 처리할지.</summary>
public enum DeleteMode
{
    /// <summary>원본을 그대로 둔다.</summary>
    None,

    /// <summary>휴지통으로 보낸다. 되살리기 쉽지만 휴지통을 비우기 전까지 용량은 그대로다.</summary>
    RecycleBin,

    /// <summary>즉시 영구 삭제한다. 용량이 바로 확보된다.</summary>
    Permanent
}
