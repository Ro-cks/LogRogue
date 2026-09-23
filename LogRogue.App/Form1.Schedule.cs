using LogRogue.Core;
using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.History;
using LogRogue.Core.Scheduling;

namespace LogRogue.App;

/// <summary>
/// Form1의 예약 실행 부분.
///
/// 30초마다 시각을 확인해서 때가 되면 스스로 백업을 실행한다.
/// 긴 간격의 타이머 하나로 기다리지 않고 짧게 반복 확인하는 이유는
/// 절전에서 깨어나거나 시스템 시각이 바뀌어도 알아서 따라잡기 때문이다.
///
/// 예약 실행은 확인 창을 띄우지 않고 설정대로 실행한다.
/// 사람이 없을 때 도는 것이므로, 결과는 작업 이력과 로그 파일에 남는다.
/// </summary>
public partial class Form1
{
    /// <summary>예약 시각을 확인하는 주기.</summary>
    private const int ScheduleCheckIntervalMs = 30_000;

    private System.Windows.Forms.Timer _scheduleTimer = null!;
    private ScheduleSettings _schedule = new();

    /// <summary>프로그램이 켜진 뒤 첫 확인인지. 놓친 예약 판단에 쓴다.</summary>
    private bool _startupCheckPending = true;

    private void SetupSchedule()
    {
        _scheduleTimer = new System.Windows.Forms.Timer { Interval = ScheduleCheckIntervalMs };
        _scheduleTimer.Tick += (_, _) => OnScheduleTick();
        _scheduleTimer.Start();

        components ??= new System.ComponentModel.Container();
        components.Add(_scheduleTimer);

        EnsureScheduleAnchor();
        UpdateScheduleDisplay();

        // 첫 확인은 곧바로 하지 않고 첫 타이머(30초 뒤)에 맡긴다.
        // 부팅 직후에는 네트워크 드라이브가 아직 연결되지 않았을 수 있다.
    }

    private void OnScheduleTick()
    {
        CheckSchedule(_startupCheckPending);
        _startupCheckPending = false;
    }

    /// <summary>
    /// 예약을 켠 시점을 기준 시각으로 박아둔다.
    ///
    /// 다음 실행 시각은 기준 시각 이후 처음 오는 예약 시각으로 계산한다.
    /// 기준이 없어 "지금"을 쓰면 계산 결과가 언제나 미래가 되어 영원히 실행되지 않는다.
    /// 실제로 실행되고 나면 그 시각이 새 기준이 된다.
    /// </summary>
    private void EnsureScheduleAnchor()
    {
        if (!_schedule.Enabled || _schedule.LastRunAt is not null)
            return;

        _schedule.LastRunAt = DateTime.Now;
        TrySaveSettings();
    }

    /// <summary>예약 시각이 됐는지 확인하고, 됐으면 실행한다.</summary>
    private void CheckSchedule(bool startup)
    {
        if (!_schedule.Enabled || _isRunning || _dialogOpen)
            return;   // 작업 중이거나 창이 떠 있으면 다음 확인 때 다시 본다

        DateTime now = DateTime.Now;

        if (!ScheduleCalculator.IsDue(_schedule, now))
            return;

        if (ScheduleCalculator.IsMissed(_schedule, now) && !_schedule.CatchUpMissed)
        {
            _log.Info($"지나간 예약을 건너뜁니다. ({(startup ? "시작 시 확인" : "실행 중 확인")})");
            MarkScheduleRun();
            UpdateScheduleDisplay();
            return;
        }

        RunScheduled();
    }

    /// <summary>예약 실행. 확인 창 없이 설정대로 바로 실행한다.</summary>
    private async void RunScheduled()
    {
        string sourceRoot = txtSourcePath.Text.Trim();
        string outputDirectory = txtOutputPath.Text.Trim();
        BackupPeriod period = SelectedPeriod();
        ArchiveGrouping grouping = SelectedGrouping();
        DeleteMode deleteMode = SelectedDeleteMode();

        _log.Info($"예약 실행 시작 ({ScheduleCalculator.Describe(_schedule)})");

        try
        {
            SetBusy(true);
            ShowStatus("예약 실행: 대상 폴더를 확인하는 중...", Color.Black);

            BackupPlan plan = await Task.Run(
                () => _job.Prepare(sourceRoot, outputDirectory, period, grouping));

            if (plan.Days.Count == 0)
            {
                _log.Info("예약 실행: 대상 폴더가 없어 넘어갑니다.");
                ShowStatus($"예약 실행: 대상 폴더가 없습니다. ({DateTime.Now:MM-dd HH:mm})", Color.DimGray);
            }
            else
            {
                await ExecuteBackupAsync(plan, deleteMode, RunTrigger.Scheduled);
            }
        }
        catch (Exception ex)
        {
            _log.Error("예약 실행에 실패했습니다.", ex);
            ShowStatus($"예약 실행 실패: {ex.Message}", Color.Firebrick);
            NotifyFromTray($"{AppName} - 예약 실행 실패", ex.Message, ToolTipIcon.Warning);
        }
        finally
        {
            // 실패했더라도 이번 예약은 끝난 것으로 본다.
            // 그러지 않으면 30초마다 같은 실패를 반복한다.
            MarkScheduleRun();
            SetBusy(false);
            UpdateScheduleDisplay();
        }
    }

    /// <summary>이번 예약을 처리했다고 기록한다. 다음 실행 시각 계산의 기준이 된다.</summary>
    private void MarkScheduleRun()
    {
        _schedule.LastRunAt = DateTime.Now;
        TrySaveSettings();
    }

    // ── 예약 설정 창 ─────────────────────────────────────

    /// <summary>예약 설정 창을 연다. 트레이 메뉴에서도 호출된다. (Form1.Tray.cs)</summary>
    private void OpenScheduleSettings()
    {
        if (_dialogOpen)
            return;

        using var dialog = new ScheduleForm(_schedule.Clone(), SelectedDeleteMode());

        _dialogOpen = true;
        DialogResult answer = dialog.ShowDialog(this);
        _dialogOpen = false;

        if (answer != DialogResult.OK)
            return;

        _schedule = dialog.Result;
        EnsureScheduleAnchor();
        TrySaveSettings();

        _log.Info($"예약 설정 변경: {ScheduleCalculator.Describe(_schedule)}");
        UpdateScheduleDisplay();
        ShowStatus(ScheduleStatusText(), Color.DimGray);
    }

    // ── 표시 ─────────────────────────────────────────────

    /// <summary>트레이 아이콘 툴팁에 다음 실행 시각을 반영한다.</summary>
    private void UpdateScheduleDisplay()
    {
        if (_trayIcon is null)
            return;

        DateTime? next = ScheduleCalculator.NextRun(_schedule, DateTime.Now);

        _trayIcon.Text = _schedule.Enabled && next is not null
            ? $"{AppName} - 다음 실행 {next:MM-dd HH:mm}"
            : AppName;
    }

    private string ScheduleStatusText()
    {
        if (!_schedule.Enabled)
            return "예약 실행을 껐습니다.";

        DateTime? next = ScheduleCalculator.NextRun(_schedule, DateTime.Now);

        return next is null
            ? "예약 요일이 선택되지 않아 실행되지 않습니다."
            : $"{ScheduleCalculator.Describe(_schedule)}  ·  다음 실행: {next:yyyy-MM-dd HH:mm}";
    }

    private void NotifyFromTray(string title, string message, ToolTipIcon icon)
        => _trayIcon?.ShowBalloonTip(5000, title, message, icon);
}