using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.History;
using LogRogue.Core.Scanning;

namespace LogRogue.App;

/// <summary>
/// 지난 백업 실행 기록을 보여주는 창.
/// 위쪽 목록에서 한 건을 고르면 아래에 자세한 내용이 나온다.
///
/// PreviewForm과 마찬가지로 디자이너 없이 코드로 만든 창이다.
/// </summary>
[DesignerCategory("Code")]
public sealed class HistoryForm : Form
{
    private readonly IReadOnlyList<RunHistoryEntry> _entries;
    private readonly string _logDirectory;
    private readonly ListView _list = new();
    private readonly TextBox _detail = new();

    public HistoryForm(IReadOnlyList<RunHistoryEntry> entries, string logDirectory)
    {
        _entries = entries;
        _logDirectory = logDirectory;

        SuspendLayout();

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;

        Text = "작업 이력";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 500);
        MinimumSize = new Size(560, 380);
        MinimizeBox = false;
        ShowInTaskbar = false;
        Padding = new Padding(12);

        BuildList();
        BuildDetail();

        FlowLayoutPanel buttons = CreateButtons(out Button closeButton);

        // 목록(Fill)을 먼저 넣어야 나머지 공간을 차지한다
        Controls.Add(_list);
        Controls.Add(_detail);
        Controls.Add(buttons);

        CancelButton = closeButton;

        ResumeLayout(false);
        PerformLayout();
    }

    private void BuildList()
    {
        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.GridLines = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;

        _list.Columns.Add("실행 시각", 135);
        _list.Columns.Add("소요", 60, HorizontalAlignment.Right);
        _list.Columns.Add("방식", 55);
        _list.Columns.Add("단위", 60);
        _list.Columns.Add("압축", 165);
        _list.Columns.Add("원본 삭제", 85);
        _list.Columns.Add("결과", 110);

        _list.BeginUpdate();

        foreach (RunHistoryEntry entry in _entries)
        {
            var item = new ListViewItem(new[]
            {
                entry.StartedAt.ToString("yyyy-MM-dd HH:mm"),
                FormatDuration(entry.Duration),
                TriggerText(entry.Trigger),
                GroupingText(entry.Grouping),
                $"{entry.ArchivedGroups}개 · {ByteSize.ToDisplay(entry.OriginalBytes)} → {ByteSize.ToDisplay(entry.ArchiveBytes)}",
                DeleteText(entry),
                entry.HasProblem ? $"문제 {entry.Problems.Count}건" : "정상"
            });

            if (entry.HasProblem)
                item.ForeColor = Color.DarkOrange;

            _list.Items.Add(item);
        }

        _list.EndUpdate();
        _list.SelectedIndexChanged += (_, _) => ShowDetail();

        if (_list.Items.Count > 0)
            _list.Items[0].Selected = true;
    }

    private void BuildDetail()
    {
        _detail.Dock = DockStyle.Bottom;
        _detail.Multiline = true;
        _detail.ReadOnly = true;
        _detail.ScrollBars = ScrollBars.Vertical;
        _detail.Height = 120;
        _detail.BackColor = SystemColors.Window;

        _detail.Text = _entries.Count == 0
            ? "아직 실행 기록이 없습니다."
            : "";
    }

    private void ShowDetail()
    {
        if (_list.SelectedIndices.Count == 0)
            return;

        RunHistoryEntry entry = _entries[_list.SelectedIndices[0]];

        var text = new StringBuilder();
        text.AppendLine($"기간: {entry.Period}");
        text.AppendLine($"대상 폴더: {entry.SourceRoot}");
        text.AppendLine($"출력 폴더: {entry.OutputDirectory}");
        text.AppendLine(
            $"압축 파일 {entry.ArchivedGroups}개 ({entry.ArchivedDays}일)" +
            (entry.FailedGroups > 0 ? $" · 실패 {entry.FailedGroups}개" : "") +
            $" · 원본 삭제 {entry.DeletedDays}일" +
            (entry.FailedDeletions > 0 ? $" · 삭제 실패 {entry.FailedDeletions}일" : ""));

        if (entry.Problems.Count > 0)
        {
            text.AppendLine();
            foreach (string problem in entry.Problems)
                text.AppendLine(problem);
        }

        _detail.Text = text.ToString();
        _detail.SelectionStart = 0;
        _detail.ScrollToCaret();
    }

    private FlowLayoutPanel CreateButtons(out Button closeButton)
    {
        closeButton = new Button
        {
            Text = "닫기",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(90, 28)
        };

        var logButton = new Button
        {
            Text = "로그 폴더 열기",
            AutoSize = true,
            MinimumSize = new Size(120, 28)
        };
        logButton.Click += (_, _) => OpenLogFolder();

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };

        panel.Controls.Add(closeButton);
        panel.Controls.Add(logButton);
        return panel;
    }

    private void OpenLogFolder()
    {
        if (!Directory.Exists(_logDirectory))
        {
            MessageBox.Show(this, "아직 로그 폴더가 만들어지지 않았습니다.", "작업 이력",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = _logDirectory, UseShellExecute = true });
    }

    // ── 표시용 문자열 ────────────────────────────────────

    private static string FormatDuration(TimeSpan duration)
        => duration.TotalMinutes >= 1
            ? $"{(int)duration.TotalMinutes}분 {duration.Seconds}초"
            : $"{duration.TotalSeconds:0.0}초";

    private static string TriggerText(RunTrigger trigger) => trigger switch
    {
        RunTrigger.Tray => "트레이",
        RunTrigger.Scheduled => "예약",
        _ => "수동"
    };

    private static string GroupingText(ArchiveGrouping grouping) => grouping switch
    {
        ArchiveGrouping.Weekly => "주별",
        ArchiveGrouping.Monthly => "월별",
        ArchiveGrouping.WholeRange => "전체",
        _ => "일별"
    };

    private static string DeleteText(RunHistoryEntry entry) => entry.DeleteMode switch
    {
        DeleteMode.RecycleBin => $"휴지통 {entry.DeletedDays}일",
        DeleteMode.Permanent => $"영구 {entry.DeletedDays}일",
        _ => "안 함"
    };
}
