using LogRogue.Core;
using LogRogue.Core.Diagnostics;
using LogRogue.Core.Startup;

namespace LogRogue.App;

internal static class Program
{
    // 개발용 빌드(Debug)와 배포용 빌드(Release)는 다른 이름을 써서 서로 막지 않게 한다.
    // 같은 이름이면 설치해둔 LogRogue가 트레이에 떠 있는 동안 Visual Studio에서
    // 실행해도 새 빌드는 뜨지 않고 예전 프로그램 창만 튀어나와 헷갈리게 된다.
#if DEBUG
    private const string InstanceId = "LogRogue-3B7E2C91-Debug";
#else
    private const string InstanceId = "LogRogue-3B7E2C91";
#endif

    /// <summary>프로그램 시작 지점.</summary>
    /// <param name="args">실행할 때 붙은 인수. 자동 실행 시에는 --tray가 들어온다.</param>
    [STAThread]
    private static void Main(string[] args)
    {
        // using: Main이 끝날 때(프로그램 종료 시) Mutex를 놓아준다
        using var instance = new SingleInstance(InstanceId);

        if (!instance.IsFirst)
        {
            // 이미 실행 중이다. 기존 창을 띄우라고 알리고 이쪽은 조용히 끝낸다.
            instance.SignalFirstInstance();
            return;
        }

        ApplicationConfiguration.Initialize();

        bool startInTray = args.Any(a =>
            string.Equals(a, AutoStartRegistration.TrayArgument, StringComparison.OrdinalIgnoreCase));

        var log = new FileLog(AppPaths.LogsDirectory);
        log.Info($"프로그램 시작 ({(startInTray ? "트레이" : "창")})");

        var form = new Form1(startInTray, log);

        // 창의 핸들(Windows가 창을 식별하는 번호)이 생긴 뒤에야 다른 스레드에서 창을 다룰 수 있다.
        // 그 전에 도착한 신호는 사라지지 않고 기다리고 있다가, 듣기 시작하는 순간 전달된다.
        form.HandleCreated += (_, _) => instance.StartListening(() => BringToFront(form));

        Application.Run(form);

        log.Info("프로그램 종료");
    }

    /// <summary>백그라운드 스레드에서 불린다. 창 조작은 BeginInvoke로 UI 스레드에 맡긴다.</summary>
    private static void BringToFront(Form1 form)
    {
        try
        {
            if (!form.IsDisposed && form.IsHandleCreated)
                form.BeginInvoke((Action)form.ActivateFromOtherInstance);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // 프로그램이 막 종료되는 중이면 무시한다
        }
    }
}
