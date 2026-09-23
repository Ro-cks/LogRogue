namespace LogRogue.Core;

/// <summary>
/// 프로그램이 쓰는 파일 위치를 한곳에서 정한다.
/// 모두 %APPDATA%\LogRogue 아래에 모인다. 사용자별 폴더라 쓰기 권한 문제가 없다.
/// </summary>
public static class AppPaths
{
    public static string RootDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LogRogue");

    /// <summary>설정 파일.</summary>
    public static string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    /// <summary>프로그램 자체 동작 로그가 쌓이는 폴더.</summary>
    public static string LogsDirectory => Path.Combine(RootDirectory, "logs");

    /// <summary>작업 이력 파일. 한 줄에 실행 한 건.</summary>
    public static string HistoryFile => Path.Combine(RootDirectory, "history.jsonl");
}
