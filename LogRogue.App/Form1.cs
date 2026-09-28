using LogRogue.Core;
using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.Diagnostics;
using LogRogue.Core.History;
using LogRogue.Core.Jobs;
using LogRogue.Core.Scanning;
using LogRogue.Core.Scheduling;
using LogRogue.Core.Settings;
using LogRogue.Core.Startup;

namespace LogRogue.App;

/// <summary>
/// 메인 창. 작업 목록을 보여주고, 작업을 추가·편집·삭제·실행한다.
///
/// 이 클래스는 세 파일로 나뉘어 있다.
///   Form1.cs          작업 목록, 수동 실행, 결과 표시, 설정, 자동 실행
///   Form1.Tray.cs     트레이 상주와 닫기 처리
///   Form1.Schedule.cs 예약 실행
/// </summary>
public partial class Form1 : Form
{
    // 목록 컬럼 번호
    private const int ColumnName = 0;
    private const int ColumnSource = 1;
    private const int ColumnSchedule = 2;
    private const int ColumnNext = 3;
    private const int ColumnLast = 4;

    private readonly BackupJob _runner;
    private readonly IAppLog _log;
    private readonly SettingsStore _settingsStore = new();
    private readonly RunHistoryStore _historyStore = new();

    /// <summary>
    /// 상태 표시줄에 마우스를 올렸을 때 전체 내용을 보여주는 풍선 도움말.
    /// 라벨 폭이 고정이라 긴 메시지는 말줄임표로 잘리기 때문이다.
    /// </summary>
    private readonly ToolTip _statusTip = new()
    {
        InitialDelay = 400,
        AutoPopDelay = 20000,
        ReshowDelay = 100
    };

    private AppSettings _settings = new();

    /// <summary>스캔·압축·삭제 중인지.</summary>
    private bool _isRunning;

    /// <summary>편집 창이나 확인 창이 떠 있는지. 이때는 예약 실행과 트레이 동작을 미룬다.</summary>
    private bool _dialogOpen;

    private AutoStartRegistration? _autoStart;

    /// <summary>코드에서 체크박스 값을 바꾸는 중인지. 이때는 레지스트리를 건드리지 않는다.</summary>
    private bool _updatingAutoStartCheckbox;

    private ContextMenuStrip _jobMenu = null!;

    /// <summary>디자이너가 사용하는 기본 생성자.</summary>
    public Form1() : this(startInTray: false, NullLog.Instance) { }

    /// <param name="startInTray">true면 창을 띄우지 않고 트레이에만 뜬다. (부팅 자동 실행)</param>
    /// <param name="log">프로그램 동작 기록.</param>
    public Form1(bool startInTray, IAppLog log)
    {
        _startHidden = startInTray;   // InitializeComponent보다 먼저 정해야 창이 번쩍 떴다 사라지지 않는다
        _log = log;
        _runner = new BackupJob(log);

        InitializeComponent();

        // 창 제목과 크기 고정은 디자이너 값과 상관없이 여기서 확정한다
        Text = AppName;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        lblStatus.Text = "";
        components ??= new System.ComponentModel.Container();
        components.Add(_statusTip);

        SetupJobList();
        SetupResultList();
        SetupTray();       // Form1.Tray.cs
        LoadSettings();
        SetupAutoStart();

        btnAddJob.Click += (_, _) => AddJob();
        btnEditJob.Click += (_, _) => EditSelectedJob();
        btnRemoveJob.Click += (_, _) => RemoveSelectedJob();
        btnRunJob.Click += (_, _) => RunSelectedJob();
        btnHistory.Click += (_, _) => OpenHistory();
        btnAbout.Click += (_, _) => OpenAbout();

        SetupSchedule();   // Form1.Schedule.cs
    }

    // ── 작업 목록 ────────────────────────────────────────

    private void SetupJobList()
    {
        lvJobs.View = View.Details;
        lvJobs.FullRowSelect = true;
        lvJobs.GridLines = true;
        lvJobs.MultiSelect = false;
        lvJobs.HideSelection = false;
        lvJobs.CenterColumnHeaders();

        lvJobs.Columns.Clear();
        lvJobs.Columns.Add("작업", 110);
        lvJobs.Columns.Add("대상 폴더", 190);
        lvJobs.Columns.Add("예약", 120);
        lvJobs.Columns.Add("다음 실행", 85);
        lvJobs.Columns.Add("마지막 결과", 130);

        lvJobs.SelectedIndexChanged += (_, _) => UpdateJobButtons();
        lvJobs.DoubleClick += (_, _) => EditSelectedJob();
        lvJobs.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
                EditSelectedJob();
            else if (e.KeyCode == Keys.Delete)
                RemoveSelectedJob();
        };

        // 목록에서 오른쪽 클릭
        _jobMenu = new ContextMenuStrip();
        var editItem = new ToolStripMenuItem("편집", null, (_, _) => EditSelectedJob());
        editItem.Font = new Font(_jobMenu.Font, FontStyle.Bold);

        _jobMenu.Items.AddRange(new ToolStripItem[]
        {
            editItem,
            new ToolStripMenuItem("지금 실행", null, (_, _) => RunSelectedJob()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("대상 폴더 열기", null, (_, _) => OpenFolder(SelectedJob()?.SourcePath ?? "")),
            new ToolStripMenuItem("출력 폴더 열기", null, (_, _) => OpenFolder(SelectedJob()?.OutputPath ?? "")),
            new ToolStripSeparator(),
            new ToolStripMenuItem("삭제", null, (_, _) => RemoveSelectedJob())
        });

        _jobMenu.Opening += (_, e) =>
        {
            // 작업을 고르지 않았거나 실행 중이면 메뉴를 띄우지 않는다
            e.Cancel = SelectedJob() is null || _isRunning || _dialogOpen;
        };

        lvJobs.ContextMenuStrip = _jobMenu;
        components!.Add(_jobMenu);
    }

    /// <summary>작업 목록을 다시 그린다. 선택은 유지한다.</summary>
    private void RefreshJobList(string? selectId = null)
    {
        selectId ??= SelectedJob()?.Id;
        Dictionary<string, RunHistoryEntry> lastResults = LoadLastResults();
        DateTime now = DateTime.Now;

        lvJobs.BeginUpdate();
        lvJobs.Items.Clear();

        foreach (JobConfig job in _settings.Jobs)
        {
            lastResults.TryGetValue(job.Id, out RunHistoryEntry? last);

            var item = new ListViewItem(new[]
            {
                job.Name,
                job.SourcePath,
                job.Schedule.Enabled ? ScheduleCalculator.Describe(job.Schedule) : "예약 안 함",
                NextRunText(job, now),
                LastResultText(last)
            })
            {
                Tag = job.Id
            };

            if (last?.HasProblem == true)
                item.ForeColor = Color.DarkOrange;

            lvJobs.Items.Add(item);

            if (job.Id == selectId)
                item.Selected = true;
        }

        lvJobs.EndUpdate();

        if (lvJobs.SelectedItems.Count == 0 && lvJobs.Items.Count > 0)
            lvJobs.Items[0].Selected = true;

        if (_settings.Jobs.Count == 0)
            ShowStatus("작업이 없습니다. [추가]를 눌러 첫 작업을 만드세요.", Color.DimGray);

        UpdateJobButtons();
    }

    /// <summary>다음 실행 칸만 갱신한다. 목록 전체를 다시 그리면 깜박이므로 예약 확인 때는 이것만 쓴다.</summary>
    private void UpdateNextRunColumn()
    {
        DateTime now = DateTime.Now;

        foreach (ListViewItem item in lvJobs.Items)
        {
            if (FindJob(item.Tag as string) is JobConfig job)
                item.SubItems[ColumnNext].Text = NextRunText(job, now);
        }
    }

    private static string NextRunText(JobConfig job, DateTime now)
    {
        DateTime? next = ScheduleCalculator.NextRun(job.Schedule, now);

        return next is null ? "-"
             : next <= now ? "곧 실행"
             : next.Value.Date == now.Date ? $"오늘 {next:HH:mm}"
             : $"{next:MM-dd HH:mm}";
    }

    private static string LastResultText(RunHistoryEntry? last)
        => last is null
            ? "-"
            : $"{last.StartedAt:MM-dd HH:mm} " + (last.HasProblem ? $"문제 {last.Problems.Count}건" : "정상");

    /// <summary>작업마다 가장 최근 실행 기록.</summary>
    private Dictionary<string, RunHistoryEntry> LoadLastResults()
    {
        var result = new Dictionary<string, RunHistoryEntry>(StringComparer.Ordinal);

        // 최근 것부터 읽으므로 처음 나오는 기록이 그 작업의 마지막 실행이다
        foreach (RunHistoryEntry entry in _historyStore.LoadRecent())
            result.TryAdd(entry.EffectiveJobId, entry);

        return result;
    }

    private JobConfig? SelectedJob()
        => lvJobs.SelectedItems.Count > 0 ? FindJob(lvJobs.SelectedItems[0].Tag as string) : null;

    private JobConfig? FindJob(string? id)
        => id is null ? null : _settings.Jobs.FirstOrDefault(j => j.Id == id);

    private void UpdateJobButtons()
    {
        bool hasSelection = SelectedJob() is not null;
        bool idle = !_isRunning;

        btnAddJob.Enabled = idle;
        btnEditJob.Enabled = idle && hasSelection;
        btnRemoveJob.Enabled = idle && hasSelection;
        btnRunJob.Enabled = idle && hasSelection;
        btnHistory.Enabled = idle;
    }

    // ── 작업 추가·편집·삭제 ─────────────────────────────

    private void AddJob()
    {
        if (_isRunning || _dialogOpen)
            return;

        JobConfig? created = ShowJobEditor(JobConfig.CreateNew(NextDefaultName()), isNew: true);
        if (created is null)
            return;

        EnsureScheduleAnchor(created);   // Form1.Schedule.cs
        _settings.Jobs.Add(created);
        TrySaveSettings();

        _log.Info($"작업 추가: {created.Name} ({created.SourcePath} → {created.OutputPath})");
        RefreshJobList(created.Id);
        UpdateScheduleDisplay();
        ShowStatus($"'{created.Name}' 작업을 추가했습니다.", Color.Black);
    }

    private void EditSelectedJob()
    {
        if (_isRunning || _dialogOpen || SelectedJob() is not JobConfig current)
            return;

        JobConfig? edited = ShowJobEditor(current, isNew: false);
        if (edited is null)
            return;

        // 주기나 시각을 바꿨다면 지금부터 새로 센다.
        // 그러지 않으면 예전 기준 시각 때문에 저장하자마자 실행될 수 있다.
        if (!SameTiming(current.Schedule, edited.Schedule))
            edited.Schedule.LastRunAt = edited.Schedule.Enabled ? DateTime.Now : null;

        EnsureScheduleAnchor(edited);

        int index = _settings.Jobs.FindIndex(j => j.Id == current.Id);
        _settings.Jobs[index] = edited;
        TrySaveSettings();

        _log.Info($"작업 편집: {edited.Name}");
        RefreshJobList(edited.Id);
        UpdateScheduleDisplay();
        ShowStatus($"'{edited.Name}' 작업을 저장했습니다.", Color.Black);
    }

    private void RemoveSelectedJob()
    {
        if (_isRunning || _dialogOpen || SelectedJob() is not JobConfig job)
            return;

        DialogResult answer = MessageBox.Show(
            this,
            $"'{job.Name}' 작업을 삭제할까요?\n\n작업 설정만 지워지고, 이미 만든 압축 파일과 작업 이력은 그대로 남습니다.",
            AppName,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
            return;

        _settings.Jobs.RemoveAll(j => j.Id == job.Id);
        TrySaveSettings();

        _log.Info($"작업 삭제: {job.Name}");
        RefreshJobList();
        UpdateScheduleDisplay();
        ShowStatus($"'{job.Name}' 작업을 삭제했습니다.", Color.DimGray);
    }

    private JobConfig? ShowJobEditor(JobConfig job, bool isNew)
    {
        using var editor = new JobForm(job.Clone(), _settings.Jobs, isNew);

        _dialogOpen = true;
        DialogResult answer = editor.ShowDialog(this);
        _dialogOpen = false;

        return answer == DialogResult.OK ? editor.Result : null;
    }

    /// <summary>작업 1, 작업 2 ... 중 아직 없는 이름.</summary>
    private string NextDefaultName()
    {
        for (int n = 1; ; n++)
        {
            string name = $"작업 {n}";
            if (!_settings.Jobs.Any(j => string.Equals(j.Name, name, StringComparison.OrdinalIgnoreCase)))
                return name;
        }
    }

    private static bool SameTiming(ScheduleSettings a, ScheduleSettings b)
        => a.Enabled == b.Enabled &&
           a.Mode == b.Mode &&
           a.TimeOfDay == b.TimeOfDay &&
           a.IntervalHours == b.IntervalHours &&
           a.Weekdays.OrderBy(d => d).SequenceEqual(b.Weekdays.OrderBy(d => d));

    // ── 수동 실행 ────────────────────────────────────────

    private void RunSelectedJob()
    {
        if (SelectedJob() is JobConfig job)
            RunJobManually(job, RunTrigger.Manual);
    }

    /// <summary>
    /// 확인 창을 거쳐 작업을 실행한다. 창의 버튼과 트레이 메뉴에서 쓴다.
    /// 이벤트 처리처럼 쓰이는 메서드라 async void다.
    /// </summary>
    private async void RunJobManually(JobConfig job, RunTrigger trigger)
    {
        if (_isRunning || _dialogOpen)
            return;

        IReadOnlyList<string> errors = JobValidator.Validate(job, _settings.Jobs);
        if (errors.Count > 0)
        {
            ShowStatus($"{job.Name}: {errors[0]}", Color.Firebrick);
            return;
        }

        try
        {
            // ── 1단계: 대상 확인 ──────────────────────────
            SetBusy(true);
            ShowStatus($"{job.Name}: 대상 파일을 확인하는 중...", Color.Black);

            BackupPlan plan = await Task.Run(
                () => _runner.Prepare(job.SourcePath, job.OutputPath, job.Period, job.Grouping));

            SetBusy(false);

            if (plan.Days.Count == 0)
            {
                ShowStatus($"{job.Name}: 해당 기간에 대상 파일이 없습니다.", Color.DimGray);
                return;
            }

            // ── 2단계: 사용자 확인 ────────────────────────
            using (var preview = new PreviewForm(plan, job.EffectiveDeleteMode, job.Name))
            {
                _dialogOpen = true;
                DialogResult answer = preview.ShowDialog(this);
                _dialogOpen = false;

                if (answer != DialogResult.OK)
                {
                    ShowStatus($"{job.Name}: 취소했습니다.", Color.DimGray);
                    return;
                }
            }

            // ── 3단계: 압축 (+ 삭제) ─────────────────────
            await ExecuteBackupAsync(job, plan, trigger);
        }
        catch (Exception ex)
        {
            _log.Error($"백업을 실행하지 못했습니다: {job.Name}", ex);
            ShowStatus($"{job.Name}: {ex.Message}", Color.Firebrick);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>계획대로 압축·삭제를 실행하고 결과를 표시·기록한다. 수동 실행과 예약 실행이 함께 쓴다.</summary>
    private async Task ExecuteBackupAsync(JobConfig job, BackupPlan plan, RunTrigger trigger)
    {
        SetBusy(true);
        lvResults.Items.Clear();

        var progress = new Progress<BackupProgress>(p => OnProgress(job, p));
        DeleteMode deleteMode = job.EffectiveDeleteMode;
        DateTime startedAt = DateTime.Now;

        IReadOnlyList<GroupBackupResult> results = await Task.Run(
            () => _runner.Run(plan, deleteMode, progress));

        SaveHistory(job, plan, deleteMode, results, startedAt, trigger);
        ShowSummary(job, results, deleteMode, trigger);
        RefreshJobList();
    }

    // ── 진행 상황과 결과 표시 ─────────────────────────────

    private void SetupResultList()
    {
        lvResults.View = View.Details;
        lvResults.FullRowSelect = true;
        lvResults.GridLines = true;
        lvResults.MultiSelect = false;
        lvResults.CenterColumnHeaders();

        lvResults.Columns.Clear();
        lvResults.Columns.Add("압축 파일", 175);
        lvResults.Columns.Add("일수", 40, HorizontalAlignment.Right);
        lvResults.Columns.Add("원본", 70, HorizontalAlignment.Right);
        lvResults.Columns.Add("압축", 70, HorizontalAlignment.Right);
        lvResults.Columns.Add("감소", 45, HorizontalAlignment.Right);
        lvResults.Columns.Add("원본 삭제", 80);
        lvResults.Columns.Add("결과", 260);
    }

    /// <summary>진행 보고를 받을 때마다 호출된다. 항상 UI 스레드에서 실행된다.</summary>
    private void OnProgress(JobConfig job, BackupProgress p)
    {
        if (p.Result is null)
        {
            ShowStatus($"{job.Name}: 처리 중 ({p.Index}/{p.Total})  {p.Group.FileName}", Color.Black);
            _trayIcon.Text = TrayText($"{AppName} - {job.Name} {p.Index}/{p.Total}");
            return;
        }

        lvResults.Items.Add(CreateResultItem(p.Result));
        lvResults.EnsureVisible(lvResults.Items.Count - 1);
    }

    private static ListViewItem CreateResultItem(GroupBackupResult result)
    {
        ArchiveResult a = result.Archive;
        ArchiveGroup group = a.Group;
        string name = group.FileName;
        string days = $"{group.Days.Count}일";
        string original = ByteSize.ToDisplay(group.TotalBytes);

        if (!a.Succeeded)
        {
            return new ListViewItem(new[] { name, days, original, "", "", "-", $"압축 실패: {a.Error}" })
            {
                ForeColor = Color.Firebrick
            };
        }

        double reduction = group.TotalBytes > 0
            ? 1.0 - (double)a.ArchiveBytes / group.TotalBytes
            : 0;

        string deleteText;
        if (result.Deletions.Count == 0)
            deleteText = "-";
        else if (result.FailedDeletions > 0)
            deleteText = $"{result.DeletedFiles}/{group.FileCount}개";
        else
            deleteText = result.Deletions[0].Result.Mode == DeleteMode.RecycleBin
                ? $"휴지통 {result.DeletedFiles}개"
                : $"영구 {result.DeletedFiles}개";

        string resultText = a.ArchivePath is not null && Path.GetFileName(a.ArchivePath) != name
            ? $"완료 → {Path.GetFileName(a.ArchivePath)}"
            : "완료";

        if (a.CarriedOverEntries > 0)
            resultText += " · 기존 zip에 합침";
        if (a.RenamedEntries > 0)
            resultText += $" · 이름이 같은 다른 파일 {a.RenamedEntries}개 보존";
        if (a.SavedSeparately)
            resultText += " · 기존 zip을 읽지 못해 따로 저장";

        DayDeletion? firstFailure = result.Deletions.FirstOrDefault(d => !d.Result.Succeeded);
        if (firstFailure is not null)
            resultText += $" · 일부 남김 {firstFailure.Day.Date:MM-dd}: {firstFailure.Result.Error}";

        var item = new ListViewItem(new[]
        {
            name,
            days,
            original,
            ByteSize.ToDisplay(a.ArchiveBytes),
            reduction.ToString("P0"),
            deleteText,
            resultText
        });

        if (result.FailedDeletions > 0 || a.SavedSeparately)
            item.ForeColor = Color.DarkOrange;

        return item;
    }

    private void ShowSummary(JobConfig job, IReadOnlyList<GroupBackupResult> results, DeleteMode deleteMode, RunTrigger trigger)
    {
        var archived = results.Where(r => r.Archive.Succeeded).ToList();
        int archiveFailed = results.Count - archived.Count;

        int archivedDays = archived.Sum(r => r.Archive.Group.Days.Count);
        long originalBytes = archived.Sum(r => r.Archive.Group.TotalBytes);
        long archiveBytes = archived.Sum(r => r.Archive.ArchiveBytes);

        string message =
            $"{job.Name}: 완료  ·  압축 파일 {archived.Count}개 ({archivedDays}일)" +
            (archiveFailed > 0 ? $"  ·  실패 {archiveFailed}개" : "") +
            $"  ·  {ByteSize.ToDisplay(originalBytes)} → {ByteSize.ToDisplay(archiveBytes)}";

        var allDeletions = results.SelectMany(r => r.Deletions).ToList();
        int deletedFiles = allDeletions.Sum(d => d.Result.DeletedFiles);
        int deleteFailed = allDeletions.Count(d => !d.Result.Succeeded);

        if (deleteMode != DeleteMode.None)
        {
            long deletedBytes = allDeletions.Sum(d => d.Result.DeletedBytes);
            message += $"\n원본 삭제 {deletedFiles}개 파일 ({ByteSize.ToDisplay(deletedBytes)})" +
                       (deleteFailed > 0 ? $"  ·  일부 남은 날짜 {deleteFailed}일" : "");

            if (deleteMode == DeleteMode.RecycleBin && deletedFiles > 0)
                message += "  ·  휴지통을 비워야 공간이 확보됩니다";
        }

        bool anyProblem = archiveFailed > 0 || deleteFailed > 0;
        ShowStatus(message, anyProblem ? Color.DarkOrange : Color.Black);

        // 창을 숨겨둔 사이에 끝났거나 예약 실행이었으면 트레이 알림으로 알려준다
        bool notify = !Visible || trigger == RunTrigger.Scheduled;

        if (notify && job.Schedule.NotifyOnlyOnProblem && !anyProblem)
            notify = false;

        if (notify)
        {
            _trayIcon.ShowBalloonTip(
                5000,
                anyProblem ? $"{job.Name} - 일부 실패" : $"{job.Name} - 완료",
                message,
                anyProblem ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }
    }

    // ── 작업 이력과 정보 ─────────────────────────────────

    /// <summary>이력 창을 연다. 트레이 메뉴에서도 호출된다.</summary>
    private void OpenHistory()
    {
        if (_dialogOpen)
            return;

        IReadOnlyList<RunHistoryEntry> entries = _historyStore.LoadRecent();

        using var history = new HistoryForm(entries, AppPaths.LogsDirectory);

        _dialogOpen = true;
        history.ShowDialog(this);
        _dialogOpen = false;
    }

    /// <summary>프로그램 정보 창을 연다. 트레이 메뉴에서도 호출된다.</summary>
    private void OpenAbout()
    {
        if (_dialogOpen)
            return;

        using var about = new AboutForm(_trayIcon.Icon);

        _dialogOpen = true;
        about.ShowDialog(this);
        _dialogOpen = false;
    }

    /// <summary>실행 결과를 이력에 남긴다. 실패해도 백업 자체에는 지장이 없다.</summary>
    private void SaveHistory(
        JobConfig job,
        BackupPlan plan,
        DeleteMode deleteMode,
        IReadOnlyList<GroupBackupResult> results,
        DateTime startedAt,
        RunTrigger trigger)
    {
        try
        {
            _historyStore.Append(RunHistoryEntry.Create(
                plan, deleteMode, results, startedAt, DateTime.Now, trigger, job.Id, job.Name));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("작업 이력을 저장하지 못했습니다.", ex);
        }
    }

    // ── 설정 ─────────────────────────────────────────────

    private void LoadSettings()
    {
        SettingsLoadResult loaded = _settingsStore.Load();
        _settings = loaded.Settings;

        // 예전 형식이었다면 새 형식으로 바로 저장해둔다
        TrySaveSettings();
        RefreshJobList();

        if (loaded.Warning is not null)
        {
            _log.Warn(loaded.Warning);
            ShowStatus(loaded.Warning, Color.DarkOrange);
        }
    }

    /// <summary>설정을 저장한다. 실패해도 백업 작업 자체에는 지장이 없으므로 기록만 남긴다.</summary>
    private void TrySaveSettings()
    {
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("설정을 저장하지 못했습니다.", ex);
        }
    }

    // ── 부팅 시 자동 실행 ────────────────────────────────
    // 자동 실행 여부는 설정 파일이 아니라 레지스트리에 있는 값이 기준이다.

    private void SetupAutoStart()
    {
        string? exePath = Environment.ProcessPath;
        if (exePath is null)
        {
            chkAutoStart.Enabled = false;
            return;
        }

        _autoStart = new AutoStartRegistration(AutoStartValueName, exePath);
        RefreshAutoStartCheckbox();

        chkAutoStart.CheckedChanged += (_, _) => OnAutoStartToggled();
    }

    private void OnAutoStartToggled()
    {
        if (_updatingAutoStartCheckbox || _autoStart is null)
            return;

        try
        {
            if (chkAutoStart.Checked)
                _autoStart.Enable();
            else
                _autoStart.Disable();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            ShowStatus($"자동 실행 설정을 바꾸지 못했습니다: {ex.Message}", Color.Firebrick);
        }

        AutoStartState state = RefreshAutoStartCheckbox();
        _log.Info($"자동 실행 설정 변경: {state}");

        if (state == AutoStartState.On)
            ShowStatus("Windows에 로그인하면 트레이에서 자동으로 실행됩니다.", Color.Black);
        else if (state == AutoStartState.Off)
            ShowStatus("자동 실행을 해제했습니다.", Color.DimGray);
    }

    private AutoStartState RefreshAutoStartCheckbox()
    {
        AutoStartState state;
        try
        {
            state = _autoStart!.GetState();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            chkAutoStart.Enabled = false;
            ShowStatus($"자동 실행 상태를 확인하지 못했습니다: {ex.Message}", Color.DarkOrange);
            return AutoStartState.Off;
        }

        _updatingAutoStartCheckbox = true;
        chkAutoStart.Checked = state is AutoStartState.On or AutoStartState.BlockedByWindows;
        _updatingAutoStartCheckbox = false;

        if (state == AutoStartState.OnElsewhere)
        {
            ShowStatus(
                $"자동 실행이 다른 위치의 {AppName}(으)로 등록되어 있습니다: {_autoStart.GetRegisteredPath()}\n" +
                "체크하면 지금 실행 중인 위치로 바뀝니다.",
                Color.DarkOrange);
        }
        else if (state == AutoStartState.BlockedByWindows)
        {
            ShowStatus(
                $"Windows 시작 앱 설정에서 {AppName}이(가) 꺼져 있어 자동 실행되지 않습니다.\n" +
                $"작업 관리자 → 시작 앱에서 {AppName}을(를) 사용으로 바꾸세요.",
                Color.DarkOrange);
        }

        return state;
    }

    // ── 공통 ─────────────────────────────────────────────

    private void SetBusy(bool busy)
    {
        _isRunning = busy;
        _trayRunItem.Enabled = !busy;
        UseWaitCursor = busy;

        UpdateJobButtons();

        if (!busy)
            UpdateScheduleDisplay();   // 대기 상태에서는 다음 예약 시각을 보여준다
    }

    private void ShowStatus(string message, Color color)
    {
        lblStatus.ForeColor = color;
        lblStatus.Text = message;

        // 라벨에 다 들어가지 않은 부분도 마우스를 올리면 볼 수 있게 한다
        _statusTip.SetToolTip(lblStatus, message);
    }

    /// <summary>트레이 툴팁 글자 수 제한에 맞춘다.</summary>
    private static string TrayText(string text) => text.Length <= 63 ? text : text[..60] + "...";
}
