namespace LogRogue.Core.Safety;

/// <summary>
/// 대상 폴더로 지정하면 위험한 위치를 막는다.
///
/// 대상 폴더 안의 모든 파일이 압축과 삭제 후보가 되므로,
/// 경로를 잘못 골랐을 때 시스템이나 개인 파일이 휩쓸리지 않도록 한다.
/// 막는 건 아래 폴더 자체나 그 상위 폴더이고, 그 아래 하위 폴더는 괜찮다.
///   예) C:\Users\me\Desktop           → 거부 (바탕 화면 자체)
///       C:\Users\me\Desktop\logs      → 허용
/// </summary>
public static class SourceRootGuard
{
    public static void EnsureSafe(string sourceRoot)
    {
        string root = Normalize(sourceRoot);

        string? driveRoot = Path.GetPathRoot(root);
        if (driveRoot is not null && string.Equals(root, Normalize(driveRoot), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("드라이브 전체는 대상 폴더로 지정할 수 없습니다. 로그가 들어 있는 폴더를 지정하세요.");

        foreach ((string name, string path) in ProtectedFolders())
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;   // 이 PC에 없는 폴더 (운영체제에 따라 비어 있을 수 있다)

            if (Normalize(path).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"대상 폴더로 지정할 수 없는 위치입니다. ({name} 또는 그 상위 폴더) 로그가 들어 있는 폴더를 더 구체적으로 지정하세요.");
        }
    }

    private static IEnumerable<(string Name, string Path)> ProtectedFolders()
    {
        yield return ("Windows 폴더", Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        yield return ("시스템 폴더", Environment.GetFolderPath(Environment.SpecialFolder.System));
        yield return ("프로그램 설치 폴더", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        yield return ("프로그램 설치 폴더", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        yield return ("프로그램 데이터 폴더", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        yield return ("사용자 폴더", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        yield return ("바탕 화면", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        yield return ("문서 폴더", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        yield return ("AppData 폴더", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        yield return ("AppData 폴더", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        yield return ("이 프로그램의 데이터 폴더", AppPaths.RootDirectory);
        yield return ("이 프로그램의 실행 폴더", AppContext.BaseDirectory);
    }

    private static string Normalize(string path)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;
}
