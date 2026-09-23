using LogRogue.Core;
using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.Diagnostics;
using LogRogue.Core.History;
using LogRogue.Core.Scheduling;
using LogRogue.Core.Scanning;
using LogRogue.Core.Settings;
using LogRogue.Core.Startup;

namespace LogRogue.App;

public partial class Form1 : Form
{
    private readonly BackupJob _job;
    private readonly IAppLog _log;
    private readonly SettingsStore _settingsStore = new();
    private readonly RunHistoryStore _historyStore = new();

    /// <summary>다음 실행이 어떻게 시작됐는지. 트레이 메뉴로 실행하면 잠깐 Tray가 된다.</summary>
    private RunTrigger _nextTrigger = RunTrigger.Manual;

    /// <summary>스캔·압축·삭제 중인지. 종료 확인에 쓴다.</summary>
    private bool _isRunning;

    /// <summary>확인 창이나 폴더 선택 창이 떠 있는지. 트레이 메뉴 동작을 막는 데 쓴다.</summary>
    private bool _dialogOpen;

    private AutoStartRegistration? _autoStart;

    /// <summary>코드에서 체크박스 값을 바꾸는 중인지. 이때는 레지스트리를 건드리지 않는다.</summary>
    private bool _updatingAutoStartCheckbox;

    /// <summary>코드에서 기간 컨트롤 값을 바꾸는 중인지. 이때는 안내 문구를 띄우지 않는다.</summary>
    private bool _updatingPeriodControls;

    /// <summary>삭제 방식 콤보박스 항목. ComboBox는 ToString() 결과를 화면에 보여준다.</summary>
    private sealed record DeleteOption(string Text, DeleteMode Mode)
    {
        public override string ToString() => Text;
    }

    /// <summary>압축 단위 콤보박스 항목.</summary>
    private sealed record GroupingOption(string Text, ArchiveGrouping Grouping)
    {
        public override string ToString() => Text;
    }

    /// <summary>기간 방식 콤보박스 항목.</summary>
    private sealed record PeriodOption(string Text, PeriodMode Mode)
    {
        public override string ToString() => Text;
    }

    /// <summary>디자이너가 사용하는 기본 생성자.</summary>
    public Form1() : this(startInTray: false, NullLog.Instance) { }

    /// <param name="startInTray">true면 창을 띄우지 않고 트레이에만 뜬다. (부팅 자동 실행)</param>
    /// <param name="log">프로그램 동작 기록.</param>
    public Form1(bool startInTray, IAppLog log)
    {
        _startHidden = startInTray;   // InitializeComponent보다 먼저 정해야 창이 번쩍 떴다 사라지지 않는다
        _log = log;
        _job = new BackupJob(log);

        InitializeComponent();

        // 기본 기간: 30일 전부터 어제까지
        dtpEndDate.Value = DateTime.Today.AddDays(-1);
        dtpStartDate.Value = DateTime.Today.AddDays(-30);

        lblStatus.Text = "";
        SetupPeriodOptions();
        SetupDeleteOptions();
        SetupGroupingOptions();
        SetupResultList();
        SetupTray();   // Form1.Tray.cs

        // 콤보박스 항목이 채워진 뒤에 설정을 적용해야 삭제 방식을 고를 수 있다
        LoadSettings();
        SetupAutoStart();

        btnHistory.Click += (_, _) => OpenHistory();
        btnSchedule.Click += (_, _) => OpenScheduleSettings();

        SetupSchedule();   // Form1.Schedule.cs
    }

    // ── 작업 이력 ────────────────────────────────────────

    /// <summary>이력 창을 연다. 트레이 메뉴에서도 호출된다. (Form1.Tray.cs)</summary>
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

    /// <summary>실행 결과를 이력에 남긴다. 실패해도 백업 자체에는 지장이 없다.</summary>
    private void SaveHistory(
        BackupPlan plan,
        DeleteMode deleteMode,
        IReadOnlyList<GroupBackupResult> results,
        DateTime startedAt,
        RunTrigger trigger)
    {
        try
        {
            _historyStore.Append(
                RunHistoryEntry.Create(plan, deleteMode, results, startedAt, DateTime.Now, trigger));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("작업 이력을 저장하지 못했습니다.", ex);
        }
    }

    // ── 부팅 시 자동 실행 ────────────────────────────────
    // 자동 실행 여부는 설정 파일이 아니라 레지스트리에 있는 값이 기준이다.
    // 사용자가 작업 관리자에서 직접 끌 수도 있으므로 매번 레지스트리를 읽어 화면에 반영한다.

    private void SetupAutoStart()
    {
        string? exePath = Environment.ProcessPath;
        if (exePath is null)
        {
            chkAutoStart.Enabled = false;
            return;
        }

        _autoStart = new AutoStartRegistration(AppName, exePath);
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

    /// <summary>레지스트리의 실제 상태를 읽어 체크박스에 반영하고, 문제가 있으면 알린다.</summary>
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
                $"자동 실행이 다른 위치의 LogRogue로 등록되어 있습니다: {_autoStart.GetRegisteredPath()}\n" +
                "체크하면 지금 실행 중인 위치로 바뀝니다.",
                Color.DarkOrange);
        }
        else if (state == AutoStartState.BlockedByWindows)
        {
            ShowStatus(
                "Windows 시작 앱 설정에서 LogRogue가 꺼져 있어 자동 실행되지 않습니다.\n" +
                "작업 관리자 → 시작 앱에서 LogRogue를 사용으로 바꾸세요.",
                Color.DarkOrange);
        }

        return state;
    }

    // ── 설정 저장·불러오기 ───────────────────────────────

    private void LoadSettings()
    {
        SettingsLoadResult loaded = _settingsStore.Load();
        ApplySettings(loaded.Settings);

        if (loaded.Warning is not null)
        {
            _log.Warn(loaded.Warning);
            ShowStatus(loaded.Warning, Color.DarkOrange);
        }
    }

    private void ApplySettings(AppSettings settings)
    {
        txtSourcePath.Text = settings.SourcePath;
        txtOutputPath.Text = settings.OutputPath;

        foreach (object item in cboDeleteMode.Items)
        {
            if (item is DeleteOption option && option.Mode == settings.DeleteMode)
            {
                cboDeleteMode.SelectedItem = item;
                break;
            }
        }

        _schedule = settings.Schedule.Clone();

        _updatingPeriodControls = true;
        nudKeepDays.Value = Math.Clamp(settings.KeepRecentDays, nudKeepDays.Minimum, nudKeepDays.Maximum);

        foreach (object item in cboPeriodMode.Items)
        {
            if (item is PeriodOption option && option.Mode == settings.PeriodMode)
            {
                cboPeriodMode.SelectedItem = item;
                break;
            }
        }
        _updatingPeriodControls = false;
        UpdatePeriodControlState();

        foreach (object item in cboGrouping.Items)
        {
            if (item is GroupingOption option && option.Grouping == settings.Grouping)
            {
                cboGrouping.SelectedItem = item;
                break;
            }
        }

        // 체크 상태를 바꾸면 CheckedChanged가 불려 콤보박스 활성 상태도 같이 맞춰진다
        chkDeleteSource.Checked = settings.DeleteSource;
    }

    /// <summary>지금 화면에 입력된 값으로 설정 객체를 만든다.</summary>
    private AppSettings CollectSettings() => new()
    {
        SourcePath = txtSourcePath.Text.Trim(),
        OutputPath = txtOutputPath.Text.Trim(),
        DeleteSource = chkDeleteSource.Checked,
        DeleteMode = cboDeleteMode.SelectedItem is DeleteOption option
            ? option.Mode
            : DeleteMode.RecycleBin,
        Grouping = SelectedGrouping(),
        PeriodMode = SelectedPeriodMode(),
        KeepRecentDays = (int)nudKeepDays.Value,
        Schedule = _schedule
    };

    /// <summary>
    /// 설정을 저장한다. 저장에 실패해도 백업 작업 자체에는 지장이 없으므로
    /// 사용자를 막지 않고 넘어간다. 나중에 작업 이력 기능이 생기면 여기서 기록을 남긴다.
    /// </summary>
    private void TrySaveSettings()
    {
        try
        {
            _settingsStore.Save(CollectSettings());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("설정을 저장하지 못했습니다.", ex);
        }
    }

    // ── 초기 설정 ────────────────────────────────────────

    private void SetupDeleteOptions()
    {
        cboDeleteMode.DropDownStyle = ComboBoxStyle.DropDownList;   // 목록에서 고르기만 가능, 직접 입력 불가
        cboDeleteMode.Items.Clear();
        cboDeleteMode.Items.Add(new DeleteOption("휴지통으로 이동", DeleteMode.RecycleBin));
        cboDeleteMode.Items.Add(new DeleteOption("영구 삭제", DeleteMode.Permanent));
        cboDeleteMode.SelectedIndex = 0;   // 기본값은 되돌릴 수 있는 휴지통

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

    // ── 기간 ─────────────────────────────────────────────

    private void SetupPeriodOptions()
    {
        nudKeepDays.Minimum = BackupPeriod.MinKeepRecentDays;
        nudKeepDays.Maximum = BackupPeriod.MaxKeepRecentDays;
        nudKeepDays.Value = 7;

        cboPeriodMode.DropDownStyle = ComboBoxStyle.DropDownList;
        cboPeriodMode.Items.Clear();
        cboPeriodMode.Items.Add(new PeriodOption("지정한 기간", PeriodMode.Absolute));
        cboPeriodMode.Items.Add(new PeriodOption("최근 며칠 제외 전부", PeriodMode.Relative));
        cboPeriodMode.SelectedIndex = 0;

        // 사용자가 직접 바꿨을 때만 계산된 날짜를 안내한다
        cboPeriodMode.SelectedIndexChanged += (_, _) => UpdatePeriodControlState(showHint: true);
        nudKeepDays.ValueChanged += (_, _) => UpdatePeriodControlState(showHint: true);

        UpdatePeriodControlState();
    }

    private PeriodMode SelectedPeriodMode()
        => cboPeriodMode.SelectedItem is PeriodOption option ? option.Mode : PeriodMode.Absolute;

    /// <summary>지금 화면에 입력된 기간.</summary>
    private BackupPeriod SelectedPeriod()
        => SelectedPeriodMode() == PeriodMode.Relative
            ? BackupPeriod.Relative((int)nudKeepDays.Value)
            : BackupPeriod.Absolute(
                DateOnly.FromDateTime(dtpStartDate.Value),
                DateOnly.FromDateTime(dtpEndDate.Value));

    /// <summary>고른 방식에 맞는 컨트롤만 쓸 수 있게 한다.</summary>
    /// <param name="showHint">상대 기간일 때 계산된 날짜를 상태 표시줄에 알릴지. 작업 결과를 덮지 않도록 기본은 알리지 않는다.</param>
    private void UpdatePeriodControlState(bool showHint = false)
    {
        bool relative = SelectedPeriodMode() == PeriodMode.Relative;

        dtpStartDate.Enabled = !relative && !_isRunning;
        dtpEndDate.Enabled = !relative && !_isRunning;
        nudKeepDays.Enabled = relative && !_isRunning;

        if (showHint && relative && !_updatingPeriodControls)
            ShowStatus(SelectedPeriod().Describe(), Color.DimGray);
    }

    private void SetupGroupingOptions()
    {
        cboGrouping.DropDownStyle = ComboBoxStyle.DropDownList;
        cboGrouping.Items.Clear();
        cboGrouping.Items.Add(new GroupingOption("일별", ArchiveGrouping.Daily));
        cboGrouping.Items.Add(new GroupingOption("주별 (월~일)", ArchiveGrouping.Weekly));
        cboGrouping.Items.Add(new GroupingOption("월별", ArchiveGrouping.Monthly));
        cboGrouping.Items.Add(new GroupingOption("선택 기간 전체", ArchiveGrouping.WholeRange));
        cboGrouping.SelectedIndex = 0;
    }

    private ArchiveGrouping SelectedGrouping()
        => cboGrouping.SelectedItem is GroupingOption option ? option.Grouping : ArchiveGrouping.Daily;

    /// <summary>결과 목록의 표시 방식과 컬럼을 설정한다.</summary>
    private void SetupResultList()
    {
        lvResults.View = View.Details;   // 이걸 안 하면 컬럼 없이 아이콘으로 나온다
        lvResults.FullRowSelect = true;
        lvResults.GridLines = true;
        lvResults.MultiSelect = false;

        lvResults.Columns.Clear();
        lvResults.Columns.Add("압축 파일", 175);
        lvResults.Columns.Add("일수", 40, HorizontalAlignment.Right);
        lvResults.Columns.Add("원본", 70, HorizontalAlignment.Right);
        lvResults.Columns.Add("압축", 70, HorizontalAlignment.Right);
        lvResults.Columns.Add("감소", 45, HorizontalAlignment.Right);
        lvResults.Columns.Add("원본 삭제", 80);
        lvResults.Columns.Add("결과", 260);
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
        BackupPeriod period = SelectedPeriod();
        DeleteMode deleteMode = SelectedDeleteMode();
        ArchiveGrouping grouping = SelectedGrouping();

        // 트레이에서 실행했는지 여기서 확정하고 원래대로 돌려둔다
        RunTrigger trigger = _nextTrigger;
        _nextTrigger = RunTrigger.Manual;

        // 실제로 실행한 값은 다음에 켤 때도 쓰이도록 바로 저장해둔다
        TrySaveSettings();

        try
        {
            // ── 1단계: 대상 확인 ──────────────────────────
            SetBusy(true);
            ShowStatus("대상 폴더를 확인하는 중...", Color.Black);

            BackupPlan plan = await Task.Run(
                () => _job.Prepare(sourceRoot, outputDirectory, period, grouping));

            SetBusy(false);

            if (plan.Days.Count == 0)
            {
                ShowStatus("해당 기간에 대상 폴더가 없습니다.", Color.DimGray);
                return;
            }

            // ── 2단계: 사용자 확인 ────────────────────────
            // using: 창을 닫은 뒤 창이 쓰던 자원을 바로 정리한다
            using (var preview = new PreviewForm(plan, deleteMode))
            {
                _dialogOpen = true;
                DialogResult answer = preview.ShowDialog(this);
                _dialogOpen = false;

                if (answer != DialogResult.OK)
                {
                    ShowStatus("취소했습니다.", Color.DimGray);
                    return;
                }
            }

            // ── 3단계: 압축 (+ 삭제) ─────────────────────
            await ExecuteBackupAsync(plan, deleteMode, trigger);
        }
        catch (Exception ex)
        {
            _log.Error("백업을 실행하지 못했습니다.", ex);
            ShowStatus(ex.Message, Color.Firebrick);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>계획대로 압축·삭제를 실행하고 결과를 표시·기록한다. 수동 실행과 예약 실행이 함께 쓴다.</summary>
    private async Task ExecuteBackupAsync(BackupPlan plan, DeleteMode deleteMode, RunTrigger trigger)
    {
        SetBusy(true);
        lvResults.Items.Clear();

        // Progress는 만든 스레드(UI 스레드)로 알아서 보고를 넘겨준다
        var progress = new Progress<BackupProgress>(OnProgress);

        DateTime startedAt = DateTime.Now;

        IReadOnlyList<GroupBackupResult> results = await Task.Run(
            () => _job.Run(plan, deleteMode, progress));

        SaveHistory(plan, deleteMode, results, startedAt, trigger);
        ShowSummary(results, deleteMode, trigger);
    }

    // ── 진행 상황과 결과 표시 ─────────────────────────────

    /// <summary>진행 보고를 받을 때마다 호출된다. 항상 UI 스레드에서 실행된다.</summary>
    private void OnProgress(BackupProgress p)
    {
        if (p.Result is null)
        {
            ShowStatus($"처리 중 ({p.Index}/{p.Total})  {p.Group.FileName}", Color.Black);
            _trayIcon.Text = $"{AppName} - 처리 중 {p.Index}/{p.Total}";
            return;
        }

        lvResults.Items.Add(CreateResultItem(p.Result));
        lvResults.EnsureVisible(lvResults.Items.Count - 1);   // 새 줄이 보이도록 스크롤
    }

    private static ListViewItem CreateResultItem(GroupBackupResult result)
    {
        ArchiveResult a = result.Archive;
        ArchiveGroup group = a.Group;
        string name = group.FileName;
        string days = $"{group.Days.Count}일";
        string original = ByteSize.ToDisplay(group.TotalBytes);

        // 압축 실패: 삭제는 시도조차 하지 않는다
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

        // 원본 삭제 칸: 삭제 안 함 "-", 전부 성공 "휴지통 7일", 일부 실패 "5/7일"
        string deleteText;
        if (result.Deletions.Count == 0)
            deleteText = "-";
        else if (result.FailedDeletions > 0)
            deleteText = $"{result.DeletedDays}/{result.Deletions.Count}일";
        else
            deleteText = result.Deletions[0].Result.Mode == DeleteMode.RecycleBin
                ? $"휴지통 {result.DeletedDays}일"
                : $"영구 {result.DeletedDays}일";

        // 결과 칸
        string resultText = a.ArchivePath is not null && Path.GetFileName(a.ArchivePath) != name
            ? $"완료 → {Path.GetFileName(a.ArchivePath)}"
            : "완료";

        if (a.CarriedOverEntries > 0)
            resultText += " · 기존 zip에 합침";
        if (a.SavedSeparately)
            resultText += " · 기존 zip을 읽지 못해 따로 저장";

        DayDeletion? firstFailure = result.Deletions.FirstOrDefault(d => !d.Result.Succeeded);
        if (firstFailure is not null)
            resultText += $" · 삭제 실패 {firstFailure.Day.Date:MM-dd}: {firstFailure.Result.Error}";

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

    private void ShowSummary(IReadOnlyList<GroupBackupResult> results, DeleteMode deleteMode, RunTrigger trigger)
    {
        var archived = results.Where(r => r.Archive.Succeeded).ToList();
        int archiveFailed = results.Count - archived.Count;

        int archivedDays = archived.Sum(r => r.Archive.Group.Days.Count);
        long originalBytes = archived.Sum(r => r.Archive.Group.TotalBytes);
        long archiveBytes = archived.Sum(r => r.Archive.ArchiveBytes);

        string message =
            $"완료  ·  압축 파일 {archived.Count}개 ({archivedDays}일)" +
            (archiveFailed > 0 ? $"  ·  실패 {archiveFailed}개" : "") +
            $"  ·  {ByteSize.ToDisplay(originalBytes)} → {ByteSize.ToDisplay(archiveBytes)}";

        var allDeletions = results.SelectMany(r => r.Deletions).ToList();
        int deletedDays = allDeletions.Count(d => d.Result.Succeeded);
        int deleteFailed = allDeletions.Count - deletedDays;

        if (deleteMode != DeleteMode.None)
        {
            long deletedBytes = allDeletions.Where(d => d.Result.Succeeded).Sum(d => d.Day.TotalBytes);
            message += $"\n원본 삭제 {deletedDays}일 ({ByteSize.ToDisplay(deletedBytes)})" +
                       (deleteFailed > 0 ? $"  ·  삭제 실패 {deleteFailed}일" : "");

            if (deleteMode == DeleteMode.RecycleBin && deletedDays > 0)
                message += "  ·  휴지통을 비워야 공간이 확보됩니다";
        }

        bool anyProblem = archiveFailed > 0 || deleteFailed > 0;
        ShowStatus(message, anyProblem ? Color.DarkOrange : Color.Black);

        // 창을 숨겨둔 사이에 끝났거나 예약 실행이었으면 트레이 알림으로 알려준다
        bool notify = !Visible || trigger == RunTrigger.Scheduled;

        if (notify && _schedule.NotifyOnlyOnProblem && !anyProblem)
            notify = false;

        if (notify)
        {
            _trayIcon.ShowBalloonTip(
                5000,
                anyProblem ? $"{AppName} - 일부 실패" : $"{AppName} - 완료",
                message,
                anyProblem ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }
    }

    // ── 공통 ─────────────────────────────────────────────

    private void SetBusy(bool busy)
    {
        _isRunning = busy;
        _trayRunItem.Enabled = !busy;
        if (!busy)
            UpdateScheduleDisplay();   // 대기 상태에서는 다음 예약 시각을 보여준다

        btnRun.Enabled = !busy;
        btnHistory.Enabled = !busy;
        btnSchedule.Enabled = !busy;
        btnBrowseSource.Enabled = !busy;
        btnBrowseOutput.Enabled = !busy;
        txtSourcePath.ReadOnly = busy;
        txtOutputPath.ReadOnly = busy;
        UpdatePeriodControlState();
        chkDeleteSource.Enabled = !busy;
        UpdateDeleteOptionState();
        cboGrouping.Enabled = !busy;
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

        _dialogOpen = true;
        DialogResult answer = folderDialog.ShowDialog(this);
        _dialogOpen = false;

        return answer == DialogResult.OK ? folderDialog.SelectedPath : null;
    }
}
