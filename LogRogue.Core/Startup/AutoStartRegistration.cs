using System.Runtime.Versioning;
using Microsoft.Win32;

namespace LogRogue.Core.Startup;

/// <summary>자동 실행 등록 상태.</summary>
public enum AutoStartState
{
    /// <summary>등록되어 있지 않다.</summary>
    Off,

    /// <summary>지금 실행 중인 이 exe로 등록되어 있다.</summary>
    On,

    /// <summary>등록은 되어 있지만 다른 위치의 exe를 가리킨다. (exe를 옮겼거나 개발용 빌드로 등록한 경우)</summary>
    OnElsewhere,

    /// <summary>등록은 되어 있지만 사용자가 작업 관리자의 시작 앱에서 꺼두었다.</summary>
    BlockedByWindows
}

/// <summary>
/// Windows 로그인 시 자동 실행 등록을 관리한다.
/// 위치: HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
/// 현재 사용자 전용 위치라 관리자 권한이 필요 없다.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AutoStartRegistration
{
    /// <summary>자동 실행 시 붙이는 인수. 이 인수로 켜지면 창 없이 트레이에만 뜬다.</summary>
    public const string TrayArgument = "--tray";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // 작업 관리자 → 시작 앱에서 "사용 안 함"을 누르면 Run 항목은 그대로 두고 여기에 표시만 남긴다
    private const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <param name="valueName">레지스트리에 등록할 이름. 작업 관리자 시작 앱 목록에 보이는 이름이기도 하다.</param>
    /// <param name="executablePath">등록할 exe의 전체 경로.</param>
    public AutoStartRegistration(string valueName, string executablePath)
    {
        ValueName = valueName;
        ExecutablePath = Path.GetFullPath(executablePath);
    }

    public string ValueName { get; }
    public string ExecutablePath { get; }

    public AutoStartState GetState()
    {
        string? registered = GetRegisteredPath();

        if (registered is null)
            return AutoStartState.Off;

        if (!PathsEqual(registered, ExecutablePath))
            return AutoStartState.OnElsewhere;

        if (IsDisabledInTaskManager())
            return AutoStartState.BlockedByWindows;

        return AutoStartState.On;
    }

    /// <summary>등록된 exe 경로. 등록되어 있지 않으면 null.</summary>
    public string? GetRegisteredPath()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string command ? ParseExecutablePath(command) : null;
    }

    /// <summary>지금 실행 중인 exe로 등록한다. 다른 위치로 등록돼 있었다면 덮어쓴다.</summary>
    public void Enable()
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        // 경로에 공백이 있어도 되도록 따옴표로 감싼다
        key.SetValue(ValueName, $"\"{ExecutablePath}\" {TrayArgument}", RegistryValueKind.String);
    }

    public void Disable()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// 레지스트리에 적힌 실행 명령에서 exe 경로만 떼어낸다.
    ///   "C:\Program Files\LogRogue\LogRogue.exe" --tray  →  C:\Program Files\LogRogue\LogRogue.exe
    ///   C:\Tools\LogRogue.exe --tray                     →  C:\Tools\LogRogue.exe
    /// </summary>
    public static string? ParseExecutablePath(string command)
    {
        command = command.Trim();
        if (command.Length == 0)
            return null;

        if (command[0] == '"')
        {
            int closing = command.IndexOf('"', 1);
            return closing > 1 ? command[1..closing] : null;
        }

        int exeEnd = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeEnd >= 0 ? command[..(exeEnd + 4)] : command;
    }

    private bool IsDisabledInTaskManager()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: false);

        // 첫 바이트가 홀수(03, 07 등)면 꺼진 상태, 짝수(02, 06 등)면 켜진 상태다
        return key?.GetValue(ValueName) is byte[] { Length: > 0 } data && (data[0] & 1) == 1;
    }

    private static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
