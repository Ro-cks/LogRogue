using System.Runtime.InteropServices;

namespace LogRogue.App;

/// <summary>
/// 프로그램이 한 번만 실행되도록 한다.
///
/// 첫 번째 실행: 이름 붙은 Mutex를 차지하고, 다른 실행이 보내는 "창 띄워" 신호를 기다린다.
/// 두 번째 실행: Mutex가 이미 있으니 신호만 보내고 바로 종료한다.
///
/// Mutex와 EventWaitHandle은 Windows가 프로세스 사이에 공유하는 이름 붙은 객체다.
/// 이름 앞의 Local\은 "현재 로그인한 사용자 세션 안에서만"이라는 뜻이다.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showSignal;
    private RegisteredWaitHandle? _listener;

    public SingleInstance(string id)
    {
        _mutex = new Mutex(initiallyOwned: true, $@"Local\{id}.Instance", out bool createdNew);
        IsFirst = createdNew;

        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{id}.Show");
    }

    /// <summary>이 실행이 첫 번째인지.</summary>
    public bool IsFirst { get; }

    /// <summary>두 번째 실행에서 호출한다. 첫 번째 실행에게 창을 띄우라고 알린다.</summary>
    public void SignalFirstInstance()
    {
        // Windows는 백그라운드 프로그램이 멋대로 창을 맨 앞으로 가져오는 걸 막는다.
        // 지금 사용자가 막 실행한 이쪽이 그 권한을 첫 번째 실행에게 넘겨준다.
        AllowSetForegroundWindow(ASFW_ANY);
        _showSignal.Set();
    }

    /// <summary>
    /// 첫 번째 실행에서 호출한다. 신호가 올 때마다 onShowRequested를 부른다.
    /// 백그라운드 스레드에서 불리므로 창을 다루려면 UI 스레드로 넘겨야 한다.
    /// 여러 번 호출해도 한 번만 등록된다.
    /// </summary>
    public void StartListening(Action onShowRequested)
    {
        if (_listener is not null)
            return;

        _listener = ThreadPool.RegisterWaitForSingleObject(
            _showSignal,
            (_, _) => onShowRequested(),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _listener?.Unregister(null);
        _showSignal.Dispose();

        if (IsFirst)
            _mutex.ReleaseMutex();

        _mutex.Dispose();
    }

    private const int ASFW_ANY = -1;

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
}
