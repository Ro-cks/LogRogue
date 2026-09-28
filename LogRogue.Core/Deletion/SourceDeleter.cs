using System.Runtime.InteropServices;
using LogRogue.Core.Archiving;
using LogRogue.Core.Scanning;

namespace LogRogue.Core.Deletion;

/// <summary>
/// 압축에 성공한 하루치 원본 파일들을 삭제한다.
///
/// 삭제는 되돌릴 수 없는 작업이므로 날짜 단위로 한 번, 파일마다 한 번 더 확인한다.
///
/// 날짜 단위 확인 (하나라도 어긋나면 그날 파일은 하나도 지우지 않는다)
///   1. 압축과 검증이 성공했고, 압축 파일이 그 크기 그대로 있으며, 이 날짜가 그 압축에 포함됐다
///   2. 오늘 이전 날짜다
///
/// 파일마다 확인 (어긋난 파일만 남기고 나머지는 지운다)
///   3. 대상 폴더 안에 있다
///   4. 링크가 아닌 실제 파일이다
///   5. 압축한 뒤로 크기와 수정한 시각이 바뀌지 않았다 (바뀌었다면 압축본에 없는 내용이 생긴 것이다)
///   6. 오늘 수정되지 않았다
///   7. 다른 프로그램이 쓰고 있지 않다
///
/// 파일을 지우고 나서 비게 된 하위 폴더도 정리한다. 대상 폴더 자체는 절대 지우지 않는다.
/// </summary>
public sealed class SourceDeleter
{
    /// <summary>예외를 던지지 않고 항상 결과를 돌려준다.</summary>
    /// <param name="day">지울 날짜.</param>
    /// <param name="archived">그 날짜가 들어간 압축 결과.</param>
    /// <param name="sourceRoot">대상 폴더.</param>
    /// <param name="today">오늘로 취급할 날짜. 생략하면 시스템 날짜를 쓴다.</param>
    public DeletionResult Delete(
        LogDay day,
        ArchiveResult archived,
        string sourceRoot,
        DeleteMode mode,
        DateOnly? today = null)
    {
        if (mode == DeleteMode.None)
            throw new ArgumentException("삭제 방식이 지정되지 않았습니다.", nameof(mode));

        DateOnly cutoff = today ?? DateOnly.FromDateTime(DateTime.Today);

        try
        {
            EnsureArchiveIsIntact(archived, day);

            if (day.Date >= cutoff)
                throw new InvalidOperationException("오늘 또는 미래 날짜는 삭제하지 않습니다.");
        }
        catch (Exception ex)
        {
            return DeletionResult.Failure(mode, ex.Message);
        }

        string root = NormalizeDirectory(sourceRoot);
        var problems = new List<string>();
        var deletable = new List<LogFile>();

        foreach (LogFile file in day.Files)
        {
            string? reason = CheckFile(file, root, cutoff, out bool alreadyGone);

            if (alreadyGone)
                continue;   // 이미 없는 파일. 압축본에는 들어 있으니 문제가 아니다.

            if (reason is null)
                deletable.Add(file);
            else
                problems.Add($"{file.RelativePath}: {reason}");
        }

        List<LogFile> deleted = mode == DeleteMode.Permanent
            ? DeletePermanently(deletable, problems)
            : MoveToRecycleBin(deletable, problems);

        RemoveEmptyFolders(deleted, root);

        return new DeletionResult
        {
            Mode = mode,
            DeletedFiles = deleted.Count,
            DeletedBytes = deleted.Sum(f => f.Length),
            Problems = problems
        };
    }

    // ── 날짜 단위 확인 ───────────────────────────────────

    private static void EnsureArchiveIsIntact(ArchiveResult archived, LogDay day)
    {
        if (!archived.Succeeded || archived.ArchivePath is null)
            throw new InvalidOperationException("압축에 성공하지 않은 파일은 삭제하지 않습니다.");

        if (!archived.Group.Days.Any(d => d.Date == day.Date))
            throw new InvalidOperationException("이 날짜가 포함되지 않은 압축 결과로는 삭제하지 않습니다.");

        var zip = new FileInfo(archived.ArchivePath);
        if (!zip.Exists || zip.Length != archived.ArchiveBytes)
            throw new InvalidOperationException("압축 파일이 없거나 크기가 바뀌어 삭제하지 않았습니다.");
    }

    // ── 파일마다 확인 ────────────────────────────────────

    /// <summary>지워도 되면 null, 안 되면 사유를 돌려준다.</summary>
    private static string? CheckFile(LogFile file, string root, DateOnly cutoff, out bool alreadyGone)
    {
        alreadyGone = false;

        string fullPath = Path.GetFullPath(file.FullPath);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return "대상 폴더 밖의 파일";

        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            alreadyGone = true;
            return null;
        }

        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return "링크 파일";

        if (DateOnly.FromDateTime(info.LastWriteTime) >= cutoff)
            return "오늘 수정됨";

        if (info.Length != file.Length || Math.Abs((info.LastWriteTime - file.LastWriteTime).TotalSeconds) > 1)
            return "압축한 뒤로 내용이 바뀜";

        try
        {
            using var _ = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        catch (IOException)
        {
            return "다른 프로그램이 사용 중";
        }
        catch (UnauthorizedAccessException)
        {
            return "접근 권한 없음";
        }

        return null;
    }

    // ── 실제 삭제 ────────────────────────────────────────

    private static List<LogFile> DeletePermanently(List<LogFile> files, List<string> problems)
    {
        var deleted = new List<LogFile>();

        foreach (LogFile file in files)
        {
            try
            {
                // 읽기 전용 파일은 지워지지 않으므로 속성을 먼저 푼다
                FileAttributes attributes = File.GetAttributes(file.FullPath);
                if (attributes.HasFlag(FileAttributes.ReadOnly))
                    File.SetAttributes(file.FullPath, attributes & ~FileAttributes.ReadOnly);

                File.Delete(file.FullPath);
                deleted.Add(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problems.Add($"{file.RelativePath}: {ex.Message}");
            }
        }

        return deleted;
    }

    /// <summary>
    /// Windows 셸 기능으로 한꺼번에 휴지통에 보낸다. 확인 창이나 오류 창은 띄우지 않는다.
    /// 네트워크 드라이브처럼 휴지통이 없는 위치이거나 휴지통 용량보다 크면
    /// Windows가 영구 삭제로 처리한다. 이 경우에도 압축 파일은 이미 검증된 상태다.
    /// 어떤 파일이 실제로 옮겨졌는지는 끝난 뒤 파일이 남아 있는지로 확인한다.
    /// </summary>
    private static List<LogFile> MoveToRecycleBin(List<LogFile> files, List<string> problems)
    {
        if (files.Count == 0)
            return new List<LogFile>();

        var operation = new ShFileOpStruct
        {
            wFunc = FO_DELETE,
            // 여러 경로를 null 문자로 이어 붙이고, 목록 끝에 null 문자가 하나 더 필요하다. (마지막 하나는 자동으로 붙는다)
            pFrom = string.Join('\0', files.Select(f => Path.GetFullPath(f.FullPath))) + '\0',
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT
        };

        SHFileOperation(ref operation);

        var moved = new List<LogFile>();
        foreach (LogFile file in files)
        {
            if (File.Exists(file.FullPath))
                problems.Add($"{file.RelativePath}: 휴지통으로 옮기지 못함");
            else
                moved.Add(file);
        }

        return moved;
    }

    /// <summary>
    /// 파일을 지운 폴더 중 비게 된 것을 아래에서부터 정리한다.
    /// 다른 파일이나 폴더가 남아 있으면 그 폴더와 그 위는 건드리지 않는다.
    /// 대상 폴더 자체는 비어도 지우지 않는다.
    /// </summary>
    private static void RemoveEmptyFolders(List<LogFile> deleted, string root)
    {
        IEnumerable<string> folders = deleted
            .Select(f => Path.GetDirectoryName(Path.GetFullPath(f.FullPath)))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => path.Length);   // 깊은 폴더부터

        foreach (string start in folders)
        {
            string? current = start;

            while (current is not null && IsStrictlyInside(current, root))
            {
                try
                {
                    if (Directory.Exists(current))
                    {
                        var info = new DirectoryInfo(current);

                        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                            info.EnumerateFileSystemInfos().Any())
                        {
                            break;   // 링크이거나 아직 뭔가 남아 있다
                        }

                        info.Delete();
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    break;   // 빈 폴더 정리는 부가 작업이라 실패해도 넘어간다
                }

                current = Path.GetDirectoryName(current);
            }
        }
    }

    private static bool IsStrictlyInside(string path, string root)
    {
        string normalized = NormalizeDirectory(path);
        return normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(normalized, root, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectory(string path)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;

    // ── Windows 휴지통 ───────────────────────────────────

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref ShFileOpStruct lpFileOp);
}
