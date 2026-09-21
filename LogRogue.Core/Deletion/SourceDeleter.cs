using System.Globalization;
using System.Runtime.InteropServices;
using LogRogue.Core.Archiving;
using LogRogue.Core.Scanning;

namespace LogRogue.Core.Deletion;

/// <summary>
/// 압축에 성공한 원본 날짜 폴더를 삭제한다.
///
/// 삭제는 되돌릴 수 없는 작업이므로 실제로 지우기 전에 아래를 모두 다시 확인한다.
/// 하나라도 어긋나면 지우지 않고 실패 결과를 돌려준다.
///   1. 압축과 검증이 성공했고, 압축 파일이 그 크기 그대로 존재한다
///   2. 지우려는 폴더가 대상 폴더 바로 아래의 날짜 폴더다
///   3. 오늘 이전 날짜다
///   4. 폴더 안의 모든 파일을 다른 프로그램이 쓰고 있지 않다
/// </summary>
public sealed class SourceDeleter
{
    private static readonly EnumerationOptions AllFiles = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = false,
        AttributesToSkip = FileAttributes.None
    };

    /// <summary>예외를 던지지 않고 항상 결과를 돌려준다.</summary>
    public DeletionResult Delete(
        ArchiveResult archived,
        string sourceRoot,
        DeleteMode mode,
        DateOnly? today = null)
    {
        if (mode == DeleteMode.None)
            throw new ArgumentException("삭제 방식이 지정되지 않았습니다.", nameof(mode));

        LogDayFolder folder = archived.Source;

        try
        {
            EnsureArchiveIsIntact(archived);
            EnsureIsDateFolderUnderRoot(folder, sourceRoot);
            EnsureIsPastDate(folder, today ?? DateOnly.FromDateTime(DateTime.Today));
            EnsureNoFileInUse(folder.Path);

            if (mode == DeleteMode.Permanent)
                DeletePermanently(folder.Path);
            else
                MoveToRecycleBin(folder.Path);

            if (Directory.Exists(folder.Path))
                return DeletionResult.Failure(mode, "일부 파일이 삭제되지 않고 남았습니다.");

            return DeletionResult.Success(mode);
        }
        catch (Exception ex)
        {
            return DeletionResult.Failure(mode, ex.Message);
        }
    }

    // ── 안전 확인 ────────────────────────────────────────

    private static void EnsureArchiveIsIntact(ArchiveResult archived)
    {
        if (!archived.Succeeded || archived.ArchivePath is null)
            throw new InvalidOperationException("압축에 성공하지 않은 폴더는 삭제하지 않습니다.");

        var zip = new FileInfo(archived.ArchivePath);
        if (!zip.Exists || zip.Length != archived.ArchiveBytes)
            throw new InvalidOperationException("압축 파일이 없거나 크기가 바뀌어 삭제하지 않았습니다.");
    }

    private static void EnsureIsDateFolderUnderRoot(LogDayFolder folder, string sourceRoot)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceRoot));
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder.Path));
        string? parent = Path.GetDirectoryName(target);

        bool directlyUnderRoot = parent is not null &&
            string.Equals(parent, root, StringComparison.OrdinalIgnoreCase);

        bool nameMatchesDate =
            DateOnly.TryParseExact(
                Path.GetFileName(target),
                FolderScanner.DateFolderFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly parsed) &&
            parsed == folder.Date;

        if (!directlyUnderRoot || !nameMatchesDate)
            throw new InvalidOperationException($"대상 폴더 바로 아래의 날짜 폴더가 아니어서 삭제하지 않았습니다: {target}");
    }

    private static void EnsureIsPastDate(LogDayFolder folder, DateOnly today)
    {
        if (folder.Date >= today)
            throw new InvalidOperationException("오늘 또는 미래 날짜 폴더는 삭제하지 않습니다.");
    }

    /// <summary>
    /// 모든 파일을 독점 모드로 열어본다. 하나라도 열리지 않으면 다른 프로그램이 쓰는 중이다.
    /// 지우다가 중간에 막혀 일부만 남는 상황을 미리 막기 위한 확인이다.
    /// </summary>
    private static void EnsureNoFileInUse(string directory)
    {
        foreach (string file in Directory.EnumerateFiles(directory, "*", AllFiles))
        {
            try
            {
                using var _ = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                throw new IOException($"사용 중인 파일이 있어 삭제하지 않았습니다: {Path.GetFileName(file)}");
            }
        }
    }

    // ── 실제 삭제 ────────────────────────────────────────

    private static void DeletePermanently(string directory)
    {
        // 읽기 전용 파일은 Directory.Delete가 지우지 못하므로 속성을 먼저 푼다
        foreach (string file in Directory.EnumerateFiles(directory, "*", AllFiles))
        {
            FileAttributes attributes = File.GetAttributes(file);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
        }

        Directory.Delete(directory, recursive: true);
    }

    /// <summary>
    /// Windows 셸 기능으로 휴지통에 보낸다. 확인 창이나 오류 창은 띄우지 않는다.
    /// 네트워크 드라이브처럼 휴지통이 없는 위치이거나 휴지통 용량보다 크면
    /// Windows가 영구 삭제로 처리한다. 이 경우에도 압축 파일은 이미 검증된 상태다.
    /// </summary>
    private static void MoveToRecycleBin(string directory)
    {
        var operation = new ShFileOpStruct
        {
            wFunc = FO_DELETE,
            pFrom = Path.GetFullPath(directory) + '\0',   // 목록 끝을 알리려면 null 문자가 두 개 필요하다. 하나는 자동으로 붙는다
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT
        };

        int code = SHFileOperation(ref operation);

        if (code != 0)
            throw new IOException($"휴지통으로 이동하지 못했습니다. (Windows 오류 코드 0x{code:X})");

        if (operation.fAnyOperationsAborted)
            throw new IOException("휴지통 이동이 중간에 중단되었습니다.");
    }

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
