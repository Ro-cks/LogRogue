using LogRogue.Core;
using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.Scanning;

namespace LogRogue.App;

public partial class Form1 : Form
{
    private readonly BackupJob _job = new();

    /// <summary>삭제 방식 콤보박스 항목. ComboBox는 ToString() 결과를 화면에 보여준다.</summary>
    private sealed record DeleteOption(string Text, DeleteMode Mode)
    {
        public override string ToString() => Text;
    }

    public Form1()
    {
        InitializeComponent();

        // 기본 기간: 30일 전부터 어제까지
        dtpEndDate.Value = DateTime.Today.AddDays(-1);
        dtpStartDate.Value = DateTime.Today.AddDays(-30);

        lblStatus.Text = "";
        SetupDeleteOptions();
        SetupResultList();
    }

    // ── 초기 설정 ────────────────────────────────────────

    private void SetupDeleteOptions()
    {
        cboDeleteMode.DropDownStyle = ComboBoxStyle.DropDownList;   // 목록에서 고르기만 가능, 직접 입력 불가
        cboDeleteMode.Items.Clear();
        cboDeleteMode.Items.Add(new DeleteOption("휴지통으로 이동", DeleteMode.RecycleBin));
        cboDeleteMode.Items.Add(new DeleteOption("영구 삭제", DeleteMode.Permanent));
        cboDeleteMode.SelectedIndex = 0;   // 기본값은 되돌릴 수 있는 휴지통

        chkDeleteSource.Checked = false;

        // 디자이너에서 더블클릭하는 대신 코드로 이벤트를 연결했다.
        // 체크박스를 켜고 끌 때마다 콤보박스 사용 가능 여부가 바뀐다.
        chkDeleteSource.CheckedChanged += (_, _) => UpdateDeleteOptionState();
        UpdateDeleteOptionState();
    }

    private void UpdateDeleteOptionState()
        => cboDeleteMode.Enabled = chkDeleteSource.Checked && chkDeleteSource.Enabled;

    private DeleteMode SelectedDeleteMode()
    {
        if (!chkDeleteSource.Checked)
            return DeleteMode.None;

        return cboDeleteMode.SelectedItem is DeleteOption option
            ? option.Mode
            : DeleteMode.None;
    }

    /// <summary>결과 목록의 표시 방식과 컬럼을 설정한다.</summary>
    private void SetupResultList()
    {
        lvResults.View = View.Details;   // 이걸 안 하면 컬럼 없이 아이콘으로 나온다
        lvResults.FullRowSelect = true;
        lvResults.GridLines = true;
        lvResults.MultiSelect = false;

        lvResults.Columns.Clear();
        lvResults.Columns.Add("날짜", 95);
        lvResults.Columns.Add("원본", 75, HorizontalAlignment.Right);
        lvResults.Columns.Add("압축", 75, HorizontalAlignment.Right);
        lvResults.Columns.Add("감소", 50, HorizontalAlignment.Right);
        lvResults.Columns.Add("원본 삭제", 75);
        lvResults.Columns.Add("결과", 300);
    }

    // ── 버튼 ─────────────────────────────────────────────

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
        DeleteMode deleteMode = SelectedDeleteMode();

        try
        {
            // ── 1단계: 대상 확인 ──────────────────────────
            SetBusy(true);
            ShowStatus("대상 폴더를 확인하는 중...", Color.Black);

            BackupPlan plan = await Task.Run(
                () => _job.Prepare(sourceRoot, outputDirectory, start, end));

            SetBusy(false);

            if (plan.Targets.Count == 0)
            {
                ShowStatus("해당 기간에 대상 폴더가 없습니다.", Color.DimGray);
                return;
            }

            // ── 2단계: 사용자 확인 ────────────────────────
            // using: 창을 닫은 뒤 창이 쓰던 자원을 바로 정리한다
            using (var preview = new PreviewForm(plan, deleteMode))
            {
                if (preview.ShowDialog(this) != DialogResult.OK)
                {
                    ShowStatus("취소했습니다.", Color.DimGray);
                    return;
                }
            }

            // ── 3단계: 압축 (+ 삭제) ─────────────────────
            SetBusy(true);
            lvResults.Items.Clear();

            // Progress는 만든 스레드(UI 스레드)로 알아서 보고를 넘겨준다
            var progress = new Progress<BackupProgress>(OnProgress);

            IReadOnlyList<DayBackupResult> results = await Task.Run(
                () => _job.Run(plan, deleteMode, progress));

            ShowSummary(results, deleteMode);
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

    // ── 진행 상황과 결과 표시 ─────────────────────────────

    /// <summary>진행 보고를 받을 때마다 호출된다. 항상 UI 스레드에서 실행된다.</summary>
    private void OnProgress(BackupProgress p)
    {
        if (p.Result is null)
        {
            ShowStatus($"처리 중 ({p.Index}/{p.Total})  {p.Folder.Date:yyyy-MM-dd}", Color.Black);
            return;
        }

        lvResults.Items.Add(CreateResultItem(p.Result));
        lvResults.EnsureVisible(lvResults.Items.Count - 1);   // 새 줄이 보이도록 스크롤
    }

    private static ListViewItem CreateResultItem(DayBackupResult day)
    {
        ArchiveResult a = day.Archive;
        DeletionResult? d = day.Deletion;
        string date = a.Source.Date.ToString("yyyy-MM-dd");
        string original = ByteSize.ToDisplay(a.Source.TotalBytes);

        // 압축 실패: 삭제는 시도조차 하지 않는다
        if (!a.Succeeded)
        {
            return new ListViewItem(new[] { date, original, "", "", "-", $"압축 실패: {a.Error}" })
            {
                ForeColor = Color.Firebrick
            };
        }

        double reduction = a.Source.TotalBytes > 0
            ? 1.0 - (double)a.ArchiveBytes / a.Source.TotalBytes
            : 0;

        string deleteText = d switch
        {
            null => "-",
            { Succeeded: false } => "실패",
            { Mode: DeleteMode.RecycleBin } => "휴지통",
            _ => "영구 삭제"
        };

        string resultText = d is { Succeeded: false }
            ? $"압축 완료, 삭제 실패: {d.Error}"
            : $"완료 → {Path.GetFileName(a.ArchivePath)}";

        var item = new ListViewItem(new[]
        {
            date,
            original,
            ByteSize.ToDisplay(a.ArchiveBytes),
            reduction.ToString("P0"),
            deleteText,
            resultText
        });

        if (d is { Succeeded: false })
            item.ForeColor = Color.DarkOrange;

        return item;
    }

    private void ShowSummary(IReadOnlyList<DayBackupResult> results, DeleteMode deleteMode)
    {
        var archived = results.Where(r => r.Archive.Succeeded).ToList();
        var deleted = results.Where(r => r.Deletion is { Succeeded: true }).ToList();

        int archiveFailed = results.Count - archived.Count;
        int deleteFailed = results.Count(r => r.Deletion is { Succeeded: false });

        long originalBytes = archived.Sum(r => r.Archive.Source.TotalBytes);
        long archiveBytes = archived.Sum(r => r.Archive.ArchiveBytes);

        string message =
            $"완료  ·  압축 {archived.Count}일" +
            (archiveFailed > 0 ? $" (실패 {archiveFailed})" : "") +
            $"  ·  {ByteSize.ToDisplay(originalBytes)} → {ByteSize.ToDisplay(archiveBytes)}";

        if (deleteMode != DeleteMode.None)
        {
            long deletedBytes = deleted.Sum(r => r.Archive.Source.TotalBytes);
            message += $"\n원본 삭제 {deleted.Count}일 ({ByteSize.ToDisplay(deletedBytes)})" +
                       (deleteFailed > 0 ? $"  ·  삭제 실패 {deleteFailed}일" : "");

            if (deleteMode == DeleteMode.RecycleBin && deleted.Count > 0)
                message += "  ·  휴지통을 비워야 공간이 확보됩니다";
        }

        bool anyProblem = archiveFailed > 0 || deleteFailed > 0;
        ShowStatus(message, anyProblem ? Color.DarkOrange : Color.Black);
    }

    // ── 공통 ─────────────────────────────────────────────

    private void SetBusy(bool busy)
    {
        btnRun.Enabled = !busy;
        btnBrowseSource.Enabled = !busy;
        btnBrowseOutput.Enabled = !busy;
        txtSourcePath.ReadOnly = busy;
        txtOutputPath.ReadOnly = busy;
        dtpStartDate.Enabled = !busy;
        dtpEndDate.Enabled = !busy;
        chkDeleteSource.Enabled = !busy;
        UpdateDeleteOptionState();
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