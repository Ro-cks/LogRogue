using System.ComponentModel;
using LogRogue.Core;
using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.Scanning;

namespace LogRogue.App;

/// <summary>
/// 압축을 시작하기 전에 만들어질 압축 파일 목록과 삭제 방식을 보여주고 확인받는 창.
/// [압축 시작]을 누르면 DialogResult.OK, [취소]나 창 닫기는 DialogResult.Cancel.
///
/// 디자이너 없이 코드로만 만든 창이다.
/// DesignerCategory("Code") 덕분에 솔루션 탐색기에서 더블클릭해도 디자이너 대신 코드가 열린다.
/// </summary>
[DesignerCategory("Code")]
public sealed class PreviewForm : Form
{
    public PreviewForm(BackupPlan plan, DeleteMode deleteMode)
    {
        SuspendLayout();

        // 디자이너가 만든 폼과 같은 방식으로 화면 배율(DPI)에 맞춰 크기가 조정되도록 한다
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;

        Text = "압축 대상 확인";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(660, 460);
        MinimumSize = new Size(480, 340);
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Padding = new Padding(12);

        int existingCount = 0;
        ListView list = CreateList(plan, ref existingCount);
        Label summary = CreateSummary(plan, existingCount);
        Label? warning = CreateDeleteWarning(deleteMode);
        FlowLayoutPanel buttons = CreateButtons(deleteMode, out Button startButton, out Button cancelButton);

        // Dock 배치는 나중에 추가한 것부터 가장자리에 붙는다.
        // 목록(Fill)을 먼저 넣어야 위아래를 뺀 나머지 공간을 채운다.
        // 위쪽은 summary가 warning보다 나중에 들어가야 맨 위에 온다.
        Controls.Add(list);
        if (warning is not null)
            Controls.Add(warning);
        Controls.Add(summary);
        Controls.Add(buttons);

        CancelButton = cancelButton;   // Esc 키 = 취소

        if (deleteMode == DeleteMode.Permanent)
        {
            // 영구 삭제일 때는 Enter를 잘못 눌러 바로 시작되지 않도록
            // Enter 키 연결을 하지 않고, 처음 포커스도 취소 버튼에 둔다
            ActiveControl = cancelButton;
        }
        else
        {
            AcceptButton = startButton;   // Enter 키 = 시작
        }

        ResumeLayout(false);
        PerformLayout();
    }

    private static Label CreateSummary(BackupPlan plan, int existingCount)
    {
        IReadOnlyList<LogDayFolder> days = plan.Days;
        int totalFiles = days.Sum(t => t.FileCount);
        long totalBytes = days.Sum(t => t.TotalBytes);

        string text =
            $"{days[0].Date:yyyy-MM-dd} ~ {days[^1].Date:yyyy-MM-dd}  ·  " +
            $"{days.Count}일  ·  파일 {totalFiles}개  ·  {ByteSize.ToDisplay(totalBytes)}\n" +
            $"압축 단위: {GroupingText(plan.Grouping)}  →  압축 파일 {plan.Groups.Count}개\n" +
            $"저장 위치: {plan.OutputDirectory}";

        if (existingCount > 0)
        {
            text += $"\n※ 같은 이름의 압축 파일이 이미 있는 {existingCount}개는 기존 내용에 새 날짜를 합쳐 다시 만듭니다.";
        }

        return new Label
        {
            Text = text,
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8)
        };
    }

    private static Label? CreateDeleteWarning(DeleteMode deleteMode)
    {
        string? text = deleteMode switch
        {
            DeleteMode.RecycleBin =>
                "원본 삭제: 휴지통으로 이동\n" +
                "압축과 검증에 성공한 날짜 폴더는 휴지통으로 이동합니다. " +
                "휴지통을 비우기 전까지 디스크 공간은 확보되지 않습니다.",

            DeleteMode.Permanent =>
                "원본 삭제: 영구 삭제\n" +
                "압축과 검증에 성공한 날짜 폴더는 영구 삭제됩니다. " +
                "되돌리려면 압축 파일에서 복원해야 합니다.",

            _ => null
        };

        if (text is null)
            return null;

        return new Label
        {
            Text = text,
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(600, 0),   // 긴 문장이 창 폭에서 줄바꿈되도록
            ForeColor = deleteMode == DeleteMode.Permanent ? Color.Firebrick : Color.DarkOrange,
            Padding = new Padding(0, 0, 0, 10)
        };
    }

    private static string GroupingText(ArchiveGrouping grouping) => grouping switch
    {
        ArchiveGrouping.Daily => "일별",
        ArchiveGrouping.Weekly => "주별 (월~일)",
        ArchiveGrouping.Monthly => "월별",
        _ => "선택 기간 전체"
    };

    /// <summary>묶음에 든 날짜 범위. 하루면 그 날짜만.</summary>
    private static string DayRangeText(ArchiveGroup group)
        => group.FirstDay == group.LastDay
            ? $"{group.FirstDay:yyyy-MM-dd}"
            : $"{group.FirstDay:yyyy-MM-dd} ~ {group.LastDay:MM-dd}";

    private static ListView CreateList(BackupPlan plan, ref int existingCount)
    {
        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false
        };

        list.Columns.Add("압축 파일", 190);
        list.Columns.Add("포함 날짜", 140);
        list.Columns.Add("일수", 45, HorizontalAlignment.Right);
        list.Columns.Add("파일 수", 60, HorizontalAlignment.Right);
        list.Columns.Add("용량", 75, HorizontalAlignment.Right);
        list.Columns.Add("비고", 100);

        list.BeginUpdate();

        foreach (ArchiveGroup group in plan.Groups)
        {
            bool exists = File.Exists(LogArchiver.GetArchivePath(group, plan.OutputDirectory));
            if (exists)
                existingCount++;

            var item = new ListViewItem(new[]
            {
                group.FileName,
                DayRangeText(group),
                $"{group.Days.Count}일",
                $"{group.FileCount}개",
                ByteSize.ToDisplay(group.TotalBytes),
                exists ? "기존 zip에 합침" : ""
            });

            if (group.FileCount == 0)
                item.ForeColor = Color.Gray;

            list.Items.Add(item);
        }

        list.EndUpdate();
        return list;
    }

    private static FlowLayoutPanel CreateButtons(
        DeleteMode deleteMode, out Button startButton, out Button cancelButton)
    {
        startButton = new Button
        {
            Text = deleteMode == DeleteMode.None ? "압축 시작" : "압축 후 삭제",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(100, 28)
        };

        cancelButton = new Button
        {
            Text = "취소",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(90, 28)
        };

        // RightToLeft 흐름이라 먼저 넣은 버튼이 가장 오른쪽에 온다
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };

        panel.Controls.Add(cancelButton);
        panel.Controls.Add(startButton);
        return panel;
    }
}
