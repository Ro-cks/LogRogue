using LogRogue.Core;
using LogRogue.Core.Archiving;
using LogRogue.Core.Scanning;

namespace LogRogue.App;

public partial class Form1 : Form
{
    private readonly BackupJob _job = new();

    public Form1()
    {
        InitializeComponent();

        // 기본 기간: 30일 전부터 어제까지
        dtpEndDate.Value = DateTime.Today.AddDays(-1);
        dtpStartDate.Value = DateTime.Today.AddDays(-30);

        // 원본 삭제는 다음 단계에서 구현한다. 그 전까지는 체크해도 동작하지 않으므로 막아둔다.
        chkDeleteSource.Enabled = false;

        lblStatus.Text = "";
        SetupResultList();
    }

    /// <summary>결과 목록의 표시 방식과 컬럼을 설정한다.</summary>
    private void SetupResultList()
    {
        lvResults.View = View.Details;   // 이걸 안 하면 컬럼 없이 아이콘으로 나온다
        lvResults.FullRowSelect = true;
        lvResults.GridLines = true;
        lvResults.MultiSelect = false;

        lvResults.Columns.Clear();
        lvResults.Columns.Add("날짜", 100);
        lvResults.Columns.Add("원본", 80, HorizontalAlignment.Right);
        lvResults.Columns.Add("압축", 80, HorizontalAlignment.Right);
        lvResults.Columns.Add("감소", 60, HorizontalAlignment.Right);
        lvResults.Columns.Add("결과", 300);
    }

    private void btnBrowseSource_Click(object sender, EventArgs e)
    {
        string? selected = PickFolder("로그가 저장된 폴더를 선택하세요.", txtSourcePath.Text);
        if (selected is not null)
            txtSourcePath.Text = selected;
    }

    private void btnBrowseOutput_Click(object sender, EventArgs e)
    {
        string? selected = PickFolder("압축 파일을 저장할 폴더를 선택하세요.", txtOutputPath.Text);
        if (selected is not null)
            txtOutputPath.Text = selected;
    }

    // 이벤트 핸들러는 async void가 허용되는 유일한 경우다.
    // await 덕분에 스캔·압축이 도는 동안에도 창이 멈추지 않는다.
    private async void btnRun_Click(object sender, EventArgs e)
    {
        // 화면 값은 반드시 UI 스레드에서 미리 읽어둔다.
        // 백그라운드 작업 안에서 컨트롤에 접근하면 오류가 난다.
        string sourceRoot = txtSourcePath.Text.Trim();
        string outputDirectory = txtOutputPath.Text.Trim();
        DateOnly start = DateOnly.FromDateTime(dtpStartDate.Value);
        DateOnly end = DateOnly.FromDateTime(dtpEndDate.Value);

        try
        {
            // ── 1단계: 대상 확인 ──────────────────────────
            SetBusy(true);
            ShowStatus("대상 폴더를 확인하는 중...", Color.Black);

            IReadOnlyList<LogDayFolder> targets = await Task.Run(
                () => _job.Prepare(sourceRoot, outputDirectory, start, end));

            SetBusy(false);

            if (targets.Count == 0)
            {
                ShowStatus("해당 기간에 대상 폴더가 없습니다.", Color.DimGray);
                return;
            }

            // ── 2단계: 사용자 확인 ────────────────────────
            // using: 창을 닫은 뒤 창이 쓰던 자원을 바로 정리한다
            using (var preview = new PreviewForm(targets, outputDirectory))
            {
                if (preview.ShowDialog(this) != DialogResult.OK)
                {
                    ShowStatus("취소했습니다.", Color.DimGray);
                    return;
                }
            }

            // ── 3단계: 압축 ──────────────────────────────
            SetBusy(true);
            lvResults.Items.Clear();

            // Progress는 만든 스레드(UI 스레드)로 알아서 보고를 넘겨준다
            var progress = new Progress<BackupProgress>(OnProgress);

            IReadOnlyList<ArchiveResult> results = await Task.Run(
                () => _job.Run(targets, outputDirectory, progress));

            ShowSummary(results);
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, Color.Firebrick);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>압축 진행 보고를 받을 때마다 호출된다. 항상 UI 스레드에서 실행된다.</summary>
    private void OnProgress(BackupProgress p)
    {
        if (p.Result is null)
        {
            ShowStatus($"압축 중 ({p.Index}/{p.Total})  {p.Folder.Date:yyyy-MM-dd}", Color.Black);
            return;
        }

        lvResults.Items.Add(CreateResultItem(p.Result));
        lvResults.EnsureVisible(lvResults.Items.Count - 1);   // 새 줄이 보이도록 스크롤
    }

    private static ListViewItem CreateResultItem(ArchiveResult r)
    {
        if (!r.Succeeded)
        {
            return new ListViewItem(new[]
            {
                r.Source.Date.ToString("yyyy-MM-dd"),
                ByteSize.ToDisplay(r.Source.TotalBytes),
                "",
                "",
                $"실패: {r.Error}"
            })
            {
                ForeColor = Color.Firebrick
            };
        }

        double reduction = r.Source.TotalBytes > 0
            ? 1.0 - (double)r.ArchiveBytes / r.Source.TotalBytes
            : 0;

        return new ListViewItem(new[]
        {
            r.Source.Date.ToString("yyyy-MM-dd"),
            ByteSize.ToDisplay(r.Source.TotalBytes),
            ByteSize.ToDisplay(r.ArchiveBytes),
            reduction.ToString("P0"),
            "완료"
        });
    }

    private void ShowSummary(IReadOnlyList<ArchiveResult> results)
    {
        var succeeded = results.Where(r => r.Succeeded).ToList();
        int failedCount = results.Count - succeeded.Count;

        long originalBytes = succeeded.Sum(r => r.Source.TotalBytes);
        long archiveBytes = succeeded.Sum(r => r.ArchiveBytes);

        string message =
            $"완료  ·  성공 {succeeded.Count}일  ·  실패 {failedCount}일  ·  " +
            $"{ByteSize.ToDisplay(originalBytes)} → {ByteSize.ToDisplay(archiveBytes)}";

        ShowStatus(message, failedCount > 0 ? Color.DarkOrange : Color.Black);
    }

    private void SetBusy(bool busy)
    {
        btnRun.Enabled = !busy;
        btnBrowseSource.Enabled = !busy;
        btnBrowseOutput.Enabled = !busy;
        txtSourcePath.ReadOnly = busy;
        txtOutputPath.ReadOnly = busy;
        dtpStartDate.Enabled = !busy;
        dtpEndDate.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void ShowStatus(string message, Color color)
    {
        lblStatus.ForeColor = color;
        lblStatus.Text = message;
    }

    private string? PickFolder(string description, string currentPath)
    {
        folderDialog.Description = description;
        folderDialog.UseDescriptionForTitle = true;

        if (Directory.Exists(currentPath))
            folderDialog.SelectedPath = currentPath;

        return folderDialog.ShowDialog(this) == DialogResult.OK
            ? folderDialog.SelectedPath
            : null;
    }
}