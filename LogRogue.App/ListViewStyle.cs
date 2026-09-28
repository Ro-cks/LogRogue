namespace LogRogue.App;

/// <summary>ListView 모양을 다듬는 도우미.</summary>
internal static class ListViewStyle
{
    /// <summary>
    /// 컬럼 제목만 가운데 정렬한다. 아래 내용의 정렬(숫자는 오른쪽 등)은 그대로 둔다.
    ///
    /// WinForms에는 제목 정렬만 따로 지정하는 기능이 없어서, 제목 행을 직접 그린다(OwnerDraw).
    /// 직접 그리기를 켜면 목록 전체가 직접 그리기로 바뀌므로, 제목을 뺀 나머지는
    /// DrawDefault로 원래 모양 그대로 그리게 넘긴다.
    ///
    /// 사용법: 컬럼을 추가하기 전이든 후든 한 번만 부르면 된다.
    ///   lvJobs.CenterColumnHeaders();
    /// </summary>
    public static void CenterColumnHeaders(this ListView list)
    {
        list.OwnerDraw = true;

        list.DrawColumnHeader += (_, e) =>
        {
            // 배경은 Windows 기본 제목 모양 그대로
            e.DrawBackground();

            TextRenderer.DrawText(
                e.Graphics,
                e.Header?.Text,
                e.Font,
                e.Bounds,
                e.ForeColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis);   // 폭이 좁으면 끝을 ...으로
        };

        // 내용 줄은 직접 그리지 않고 원래 방식 그대로
        list.DrawItem += (_, e) => e.DrawDefault = true;
        list.DrawSubItem += (_, e) => e.DrawDefault = true;
    }
}
