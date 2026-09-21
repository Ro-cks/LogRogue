using System.ComponentModel;
using LogRogue.Core.Archiving;
using LogRogue.Core.Scanning;

namespace LogRogue.App;

/// <summary>
/// 압축을 시작하기 전에 대상 날짜 폴더 목록을 보여주고 확인받는 창.
/// [압축 시작]을 누르면 DialogResult.OK, [취소]나 창 닫기는 DialogResult.Cancel.
///
/// 디자이너 없이 코드로만 만든 창이다.
/// DesignerCategory("Code") 덕분에 솔루션 탐색기에서 더블클릭해도 디자이너 대신 코드가 열린다.
/// </summary>
[DesignerCategory("Code")]
public sealed class PreviewForm : Form
{
    public PreviewForm(IReadOnlyList<LogDayFolder> targets, string outputDirectory)
    {
        SuspendLayout();

        // 디자이너가 만든 폼과 같은 방식으로 화면 배율(DPI)에 맞춰 크기가 조정되도록 한다
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;

        Text = "압축 대상 확인";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(640, 420);
        MinimumSize = new Size(480, 320);
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Padding = new Padding(12);

        int existingCount = 0;
        ListView list = CreateList(targets, outputDirectory, ref existingCount);
        Label summary = CreateSummary(targets, outputDirectory, existingCount);
        FlowLayoutPanel buttons = CreateButtons(out Button startButton, out Button cancelButton);

        // Dock 배치는 나중에 추가한 것부터 가장자리에 붙는다.
        // 목록(Fill)을 먼저 넣어야 위아래를 뺀 나머지 공간을 채운다.
        Controls.Add(list);
        Controls.Add(summary);
        Controls.Add(buttons);

        AcceptButton = startButton;   // Enter 키 = 압축 시작
        CancelButton = cancelButton;  // Esc 키 = 취소

        ResumeLayout(false);
        PerformLayout();
    }

    private static Label CreateSummary(
        IReadOnlyList<LogDayFolder> targets, string outputDirectory, int existingCount)
    {
        int totalFiles = targets.Sum(t => t.FileCount);
        long totalBytes = targets.Sum(t => t.TotalBytes);

        string text =
            $"{targets[0].Date:yyyy-MM-dd} ~ {targets[^1].Date:yyyy-MM-dd}  ·  " +
            $"{targets.Count}일  ·  파일 {totalFiles}개  ·  {ByteSize.ToDisplay(totalBytes)}\n" +
            $"저장 위치: {outputDirectory}";

        if (existingCount > 0)
            text += $"\n※ 이미 압축 파일이 있는 날짜 {existingCount}개는 새 파일로 교체됩니다.";

        return new Label
        {
            Text = text,
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 10)
        };
    }

    private static ListView CreateList(
        IReadOnlyList<LogDayFolder> targets, string outputDirectory, ref int existingCount)
    {
        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false
        };

        list.Columns.Add("날짜", 100);
        list.Columns.Add("파일 수", 70, HorizontalAlignment.Right);
        list.Columns.Add("용량", 90, HorizontalAlignment.Right);
        list.Columns.Add("비고", 110);
        list.Columns.Add("경로", 240);

        list.BeginUpdate();

        foreach (LogDayFolder folder in targets)
        {
            bool exists = File.Exists(DayFolderArchiver.GetArchivePath(folder, outputDirectory));
            if (exists)
                existingCount++;

            var item = new ListViewItem(new[]
            {
                folder.Date.ToString("yyyy-MM-dd"),
                $"{folder.FileCount}개",
                ByteSize.ToDisplay(folder.TotalBytes),
                exists ? "기존 파일 교체" : "",
                folder.Path
            });

            if (folder.FileCount == 0)
                item.ForeColor = Color.Gray;

            list.Items.Add(item);
        }

        list.EndUpdate();
        return list;
    }

    private static FlowLayoutPanel CreateButtons(out Button startButton, out Button cancelButton)
    {
        startButton = new Button
        {
            Text = "압축 시작",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(90, 28)
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
