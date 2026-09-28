using System.Reflection;

namespace LogRogue.App;

/// <summary>
/// 프로그램 정보를 실행 파일에서 읽어온다.
///
/// 값은 코드가 아니라 LogRogue.App.csproj에 적는다. 빌드할 때 실행 파일 안에 새겨지고,
/// 탐색기에서 exe 속성 → 자세히 탭에 보이는 값과 같다.
///   Product, FileVersion, Description, Company, Copyright  → 프로젝트 속성 → 패키지
///   Developer, Contact                                     → csproj의 AssemblyMetadata 항목
/// </summary>
internal static class AppInfo
{
    private static readonly Assembly Entry = Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly;

    public static string ProductName =>
        Entry.GetCustomAttribute<AssemblyProductAttribute>()?.Product is { Length: > 0 } product
            ? product
            : "로그로그";

    /// <summary>파일 버전. 예: 0.1.3.0</summary>
    public static string Version =>
        Entry.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
        ?? Entry.GetName().Version?.ToString()
        ?? "";

    public static string Description =>
        Entry.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";

    public static string Company =>
        Entry.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "";

    public static string Copyright =>
        Entry.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

    /// <summary>개발자. csproj에 AssemblyMetadata Include="Developer"로 적은 값.</summary>
    public static string Developer => Metadata("Developer");

    /// <summary>연락처. 메일 주소나 웹 주소. csproj에 AssemblyMetadata Include="Contact"로 적은 값.</summary>
    public static string Contact => Metadata("Contact");

    private static string Metadata(string key) =>
        Entry.GetCustomAttributes<AssemblyMetadataAttribute>()
             .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase))
             ?.Value ?? "";
}
