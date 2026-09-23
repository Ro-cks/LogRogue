using System.Diagnostics;
using LogRogue.Core.History;

namespace LogRogue.App;

/// <summary>
/// Form1의 트레이 관련 부분.
/// partial 클래스라 Form1.cs, Form1.Designer.cs와 합쳐져 하나의 Form1이 된다.
///
/// 동작 규칙
///   - 창의 X 버튼, Alt+F4, 최소화 → 종료하지 않고 트레이로 숨긴다
///   - 트레이 아이콘 더블클릭, 알림 클릭 → 창을 다시 연다
///   - 진짜 종료는 트레이 우클릭 메뉴의 [종료]로만 한다
///   - Windows 종료·로그오프 때는 막지 않고 바로 끝낸다
///   - --tray 인수로 켜지면(부팅 자동 실행) 창 없이 트레이에만 뜬다
/// </summary>
public partial class Form1
{
    private const string AppName = "LogRogue";

    private NotifyIcon _trayIcon = null!;
    private ContextMenuStrip _trayMenu = null!;
    private ToolStripMenuItem _trayRunItem = null!;

    /// <summary>트레이 메뉴의 [종료]를 눌렀는지. 이게 true일 때만 실제로 닫힌다.</summary>
    private bool _exitRequested;

    /// <summary>처음 트레이로 숨길 때 한 번만 안내 알림을 띄우기 위한 표시.</summary>
    private bool _trayHintShown;

    /// <summary>true인 동안은 창이 화면에 나타나지 않는다. 트레이에서 열면 false가 된다.</summary>
    private bool _startHidden;

    private void SetupTray()
    {
        _trayMenu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("열기", null, (_, _) => RestoreFromTray());
        openItem.Font = new Font(_trayMenu.Font, FontStyle.Bold);   // 더블클릭과 같은 동작이라는 표시

        _trayRunItem = new ToolStripMenuItem("지금 백업", null, (_, _) => RunFromTray());

        _trayMenu.Items.AddRange(new ToolStripItem[]
        {
            openItem,
            _trayRunItem,
            new ToolStripMenuItem("작업 이력", null, (_, _) => ShowHistoryFromTray()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("대상 폴더 열기", null, (_, _) => OpenFolder(txtSourcePath.Text)),
            new ToolStripMenuItem("출력 폴더 열기", null, (_, _) => OpenFolder(txtOutputPath.Text)),
            new ToolStripSeparator(),
            new ToolStripMenuItem("종료", null, (_, _) => ExitFromTray())
        });

        _trayIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),   // 아이콘이 없으면 트레이에 아무것도 표시되지 않는다
            Text = AppName,         // 마우스를 올렸을 때 나오는 글자
            ContextMenuStrip = _trayMenu,
            Visible = true
        };

        _trayIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                RestoreFromTray();
        };
        _trayIcon.BalloonTipClicked += (_, _) => RestoreFromTray();

        // 창 제목 표시줄 아이콘도 트레이와 같게 맞춘다
        Icon = _trayIcon.Icon;

        // 프로그램이 끝날 때 트레이 아이콘이 함께 정리되도록 폼의 구성 요소 목록에 넣는다.
        // 이걸 안 하면 종료 후에도 마우스를 올릴 때까지 트레이에 유령 아이콘이 남는다.
        components ??= new System.ComponentModel.Container();
        components.Add(_trayIcon);
        components.Add(_trayMenu);
    }

    /// <summary>
    /// 프로젝트 속성에서 지정한 실행 파일 아이콘을 가져온다.
    /// 지정하지 않았거나 가져오지 못하면 Windows 기본 프로그램 아이콘을 쓴다.
    /// </summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return SystemIcons.Application;
        }
    }

    // ── 숨기기와 복원 ────────────────────────────────────

    private void HideToTray()
    {
        // 숨긴 상태에서 PC가 꺼질 수도 있으니 지금 설정을 저장해둔다
        TrySaveSettings();
        Hide();

        if (!_trayHintShown)
        {
            _trayIcon.ShowBalloonTip(
                3000,
                AppName,
                "트레이에서 계속 실행 중입니다.\n아이콘을 더블클릭하면 다시 열립니다.",
                ToolTipIcon.Info);

            _trayHintShown = true;
        }
    }

    /// <summary>
    /// 창을 화면에 보이거나 숨길 때마다 Windows Forms가 내부적으로 부르는 메서드.
    /// 프로그램이 시작될 때 Application.Run이 창을 보이게 하려는 것도 여기를 거친다.
    /// _startHidden이면 보이지 않게 막는다. 창 핸들은 만들어둬야 트레이 알림과
    /// 중복 실행 신호 처리가 동작하므로 CreateHandle을 먼저 부른다.
    /// </summary>
    protected override void SetVisibleCore(bool value)
    {
        if (_startHidden)
        {
            if (!IsHandleCreated)
                CreateHandle();

            value = false;
        }

        base.SetVisibleCore(value);
    }

    /// <summary>이미 실행 중인데 exe를 또 실행했을 때 불린다. (Program.cs)</summary>
    public void ActivateFromOtherInstance() => RestoreFromTray();

    private void RestoreFromTray()
    {
        _startHidden = false;
        Show();

        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;

        Activate();
    }

    /// <summary>창 크기가 바뀔 때마다 호출된다. 최소화되면 트레이로 숨긴다.</summary>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        // 폼이 처음 만들어지는 도중에도 불리므로 트레이가 준비됐는지 확인한다
        if (_trayIcon is not null && WindowState == FormWindowState.Minimized)
            HideToTray();
    }

    // ── 트레이 메뉴 동작 ─────────────────────────────────

    private void RunFromTray()
    {
        RestoreFromTray();

        // 실행 버튼을 누른 것과 똑같이 동작한다. 확인 창도 똑같이 뜬다.
        if (btnRun.Enabled && !_dialogOpen)
        {
            _nextTrigger = RunTrigger.Tray;   // 이력에 트레이에서 실행했다고 남기기 위해
            btnRun.PerformClick();
        }
    }

    private void ShowHistoryFromTray()
    {
        RestoreFromTray();
        OpenHistory();   // Form1.cs
    }

    private void OpenFolder(string path)
    {
        path = path.Trim();

        if (!Directory.Exists(path))
        {
            _trayIcon.ShowBalloonTip(3000, AppName, "폴더가 지정되지 않았거나 존재하지 않습니다.", ToolTipIcon.Warning);
            return;
        }

        // UseShellExecute: 폴더 경로를 넘기면 Windows가 알아서 파일 탐색기로 연다
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void ExitFromTray()
    {
        // 확인 창이나 폴더 선택 창이 떠 있으면 그것부터 닫도록 창을 보여주기만 한다
        if (_dialogOpen)
        {
            RestoreFromTray();
            return;
        }

        _exitRequested = true;
        Close();
    }

    // ── 닫기 처리 ────────────────────────────────────────

    /// <summary>
    /// 창이 닫히려 할 때마다 호출된다.
    /// e.Cancel = true로 하면 닫히지 않는다.
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !_exitRequested)
        {
            // X 버튼, Alt+F4 → 끄지 않고 트레이로
            e.Cancel = true;
            HideToTray();
        }
        else if (_exitRequested && _isRunning && !ConfirmExitWhileRunning())
        {
            // 작업 중에 [종료]를 눌렀는데 사용자가 취소한 경우
            e.Cancel = true;
            _exitRequested = false;
        }

        // 여기까지 왔는데 취소되지 않았다면 정말 끝나는 것이다.
        // [종료] 메뉴, Windows 종료·로그오프가 여기에 해당한다.
        if (!e.Cancel)
            TrySaveSettings();

        base.OnFormClosing(e);
    }

    private bool ConfirmExitWhileRunning()
    {
        RestoreFromTray();

        DialogResult answer = MessageBox.Show(
            this,
            "백업이 진행 중입니다.\n" +
            "지금 종료하면 처리 중이던 날짜는 완료되지 않고, 다음 실행 때 다시 처리됩니다.\n\n" +
            "종료할까요?",
            AppName,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);   // 기본 선택은 [아니요]

        return answer == DialogResult.Yes;
    }
}
