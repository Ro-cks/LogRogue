using System.IO.Compression;

namespace LogRogue.Core.Archiving;

/// <summary>
/// 날짜 폴더 묶음 하나를 ZIP 파일 하나로 압축한다.
/// 예: 2026-08-17 ~ 2026-08-23 폴더들  →  D:\Backup\2026-08-17_2026-08-23.zip
///
/// 처리 순서
///   1. 묶음 안 모든 파일의 목록과 크기를 기록
///   2. 같은 이름의 압축 파일이 이미 있으면 그 내용도 목록에 합친다
///   3. 임시 파일(.zip.tmp)에 압축
///   4. 임시 파일을 다시 열어 목록과 하나하나 대조 검증
///   5. 검증 통과 시에만 최종 이름(.zip)으로 바꾼다
/// 어느 단계에서든 실패하면 임시 파일을 지우고 실패 결과를 돌려준다.
/// 기존 압축 파일과 원본은 그대로 남는다.
/// </summary>
public sealed class LogArchiver
{
    private const string TempExtension = ".tmp";

    // 스캔 때와 달리 권한 없는 항목을 건너뛰지 않는다.
    // 압축 후 원본을 지울 수도 있으므로, 하나라도 못 읽으면 묶음 전체를 실패로 처리해야 안전하다.
    private static readonly EnumerationOptions FileEnumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = false,
        AttributesToSkip = FileAttributes.None
    };

    /// <summary>압축 레벨. 텍스트 로그는 Optimal로도 90% 이상 줄어든다.</summary>
    public CompressionLevel Level { get; init; } = CompressionLevel.Optimal;

    /// <summary>
    /// 대상 폴더와 출력 폴더 조합이 올바른지 검사한다.
    /// 문제가 있으면 사용자에게 보여줄 수 있는 메시지와 함께 예외를 던진다.
    /// </summary>
    public static void ValidatePaths(string sourceRoot, string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot))
            throw new ArgumentException("대상 폴더 경로가 비어 있습니다.");

        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("출력 폴더 경로가 비어 있습니다.");

        string source = NormalizeDirectory(sourceRoot);
        string output = NormalizeDirectory(outputDirectory);

        // 출력 폴더가 대상 폴더와 같거나 그 안에 있으면
        // 만들어진 압축 파일이 다음 실행 때 대상에 섞여 들어갈 수 있다.
        if (output.StartsWith(source, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "출력 폴더는 대상 폴더와 같거나 그 안에 있을 수 없습니다. 다른 위치를 지정하세요.");
    }

    /// <summary>
    /// 묶음이 저장될 압축 파일 경로. 예: D:\Backup\2026-08.zip
    /// 압축 파일 이름 규칙은 ArchiveGrouper가 정한다.
    /// </summary>
    public static string GetArchivePath(ArchiveGroup group, string outputDirectory)
        => Path.Combine(outputDirectory, group.FileName);

    /// <summary>묶음 하나를 압축한다. 예외를 던지지 않고 항상 결과를 돌려준다.</summary>
    public ArchiveResult Archive(ArchiveGroup group, string outputDirectory)
    {
        string finalPath = GetArchivePath(group, outputDirectory);
        string tempPath = finalPath + TempExtension;

        try
        {
            Directory.CreateDirectory(outputDirectory);

            Dictionary<string, EntrySource> contents = CollectFiles(group);
            if (contents.Count == 0)
                return ArchiveResult.Failure(group, "압축할 파일이 없습니다.");

            DeleteQuietly(tempPath);   // 이전에 비정상 종료로 남은 임시 파일 정리

            string destination = finalPath;
            int carriedOver = 0;
            bool savedSeparately = false;

            // using: 쓰기가 끝나면 기존 압축 파일을 닫는다.
            // 열려 있는 상태로는 아래 File.Move가 그 파일을 교체할 수 없다.
            using (ZipArchive? existing = TryOpenExisting(finalPath, out bool existedButUnreadable))
            {
                if (existing is not null)
                {
                    carriedOver = MergeExisting(existing, contents);
                }
                else if (existedButUnreadable)
                {
                    // 기존 파일이 깨져서 합칠 수 없다. 기존 것은 건드리지 않고 따로 저장한다.
                    destination = NextFreePath(finalPath);
                    savedSeparately = true;
                }

                WriteArchive(tempPath, contents, existing);
            }

            VerifyArchive(tempPath, contents);

            // 검증을 통과한 뒤에야 최종 파일로 바꾼다.
            // 새 파일은 기존 파일의 내용을 모두 담고 있으므로 교체해도 잃는 것이 없다.
            File.Move(tempPath, destination, overwrite: true);

            return new ArchiveResult
            {
                Group = group,
                Succeeded = true,
                ArchivePath = destination,
                ArchiveBytes = new FileInfo(destination).Length,
                CarriedOverEntries = carriedOver,
                SavedSeparately = savedSeparately
            };
        }
        catch (Exception ex)
        {
            // 한 묶음이 실패해도 나머지 묶음은 계속 처리할 수 있도록
            // 예외를 결과로 바꿔서 돌려준다.
            DeleteQuietly(tempPath);
            return ArchiveResult.Failure(group, ex.Message);
        }
    }

    // ── 압축할 내용 모으기 ───────────────────────────────

    /// <summary>
    /// 압축 파일 안에 들어갈 항목 하나의 출처.
    /// FilePath가 있으면 디스크의 원본 파일, null이면 기존 압축 파일 안의 같은 이름 항목이다.
    /// </summary>
    private sealed record EntrySource(string? FilePath, long Length);

    /// <summary>
    /// 묶음 안의 모든 파일을 "압축 파일 내부 경로 → 원본"으로 정리한다.
    /// 내부 경로는 날짜 폴더명부터 시작한다. 예: 2026-08-20/globalfile/2026-08-20_global.log
    /// 이렇게 해두면 여러 날짜가 한 압축 파일에 들어가도 풀었을 때 날짜 폴더별로 나뉜다.
    /// </summary>
    private static Dictionary<string, EntrySource> CollectFiles(ArchiveGroup group)
    {
        var files = new Dictionary<string, EntrySource>(StringComparer.Ordinal);

        foreach (var day in group.Days)
        {
            string dateName = Path.GetFileName(day.Path);

            foreach (string fullPath in Directory.EnumerateFiles(day.Path, "*", FileEnumeration))
            {
                string relative = Path.GetRelativePath(day.Path, fullPath).Replace('\\', '/');
                files.Add($"{dateName}/{relative}", new EntrySource(fullPath, new FileInfo(fullPath).Length));
            }
        }

        return files;
    }

    /// <summary>
    /// 같은 이름의 압축 파일을 연다.
    /// 없으면 null(existedButUnreadable = false), 있는데 깨졌으면 null(existedButUnreadable = true).
    /// </summary>
    private static ZipArchive? TryOpenExisting(string path, out bool existedButUnreadable)
    {
        existedButUnreadable = false;

        if (!File.Exists(path))
            return null;

        try
        {
            return ZipFile.OpenRead(path);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            existedButUnreadable = true;
            return null;
        }
    }

    /// <summary>
    /// 기존 압축 파일의 항목들을 새로 압축할 목록에 합친다. 옮겨 담은 항목 수를 돌려준다.
    ///
    /// 기존에만 있는 항목: 원본이 이미 삭제된 날짜다. 그대로 옮겨 담는다.
    /// 양쪽에 다 있는 항목: 더 큰 쪽을 남긴다. 로그는 뒤에 덧붙여지기만 하므로 큰 쪽이 더 완전하다.
    /// </summary>
    private static int MergeExisting(ZipArchive existing, Dictionary<string, EntrySource> contents)
    {
        int carried = 0;

        foreach (ZipArchiveEntry entry in existing.Entries)
        {
            if (entry.Name.Length == 0)
                continue;   // 폴더 항목

            if (contents.TryGetValue(entry.FullName, out EntrySource? current) && current.Length >= entry.Length)
                continue;   // 디스크의 원본이 같거나 더 크다

            contents[entry.FullName] = new EntrySource(FilePath: null, entry.Length);
            carried++;
        }

        return carried;
    }

    // ── 쓰기와 검증 ──────────────────────────────────────

    private void WriteArchive(string zipPath, Dictionary<string, EntrySource> contents, ZipArchive? existing)
    {
        // using 블록을 벗어날 때 ZipArchive가 닫히면서 파일 끝의 목차가 기록된다.
        // 반드시 검증보다 먼저 닫혀 있어야 한다.
        using var stream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

        foreach (var (entryName, source) in contents.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            if (source.FilePath is not null)
            {
                zip.CreateEntryFromFile(source.FilePath, entryName, Level);
                continue;
            }

            // 기존 압축 파일에서 풀어서 새 압축 파일로 다시 담는다
            ZipArchiveEntry oldEntry = existing!.GetEntry(entryName)
                ?? throw new InvalidDataException($"기존 압축 파일에서 항목을 찾지 못했습니다: {entryName}");

            ZipArchiveEntry newEntry = zip.CreateEntry(entryName, Level);
            newEntry.LastWriteTime = oldEntry.LastWriteTime;

            using Stream from = oldEntry.Open();
            using Stream to = newEntry.Open();
            from.CopyTo(to);
        }
    }

    /// <summary>
    /// 만들어진 압축 파일을 다시 열어 목록과 하나하나 대조한다.
    /// 파일 개수, 이름, 그리고 실제로 끝까지 풀었을 때의 크기가 모두 일치해야 통과한다.
    /// </summary>
    private static void VerifyArchive(string zipPath, Dictionary<string, EntrySource> expected)
    {
        using ZipArchive zip = ZipFile.OpenRead(zipPath);

        var fileEntries = zip.Entries.Where(e => e.Name.Length > 0).ToList();

        if (fileEntries.Count != expected.Count)
            throw new InvalidDataException(
                $"검증 실패: 파일 개수 불일치 (예상 {expected.Count}개, 압축본 {fileEntries.Count}개)");

        foreach (ZipArchiveEntry entry in fileEntries)
        {
            if (!expected.TryGetValue(entry.FullName, out EntrySource? source))
                throw new InvalidDataException($"검증 실패: 예상에 없는 항목 {entry.FullName}");

            long actual = CountDecompressedBytes(entry);

            if (actual != source.Length)
                throw new InvalidDataException(
                    $"검증 실패: 크기 불일치 {entry.FullName} (예상 {source.Length}, 압축 해제 {actual})");
        }
    }

    /// <summary>항목을 실제로 끝까지 풀어보고 나온 바이트 수를 센다.</summary>
    private static long CountDecompressedBytes(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();

        byte[] buffer = new byte[81920];
        long total = 0;
        int read;

        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            total += read;

        return total;
    }

    // ── 보조 ─────────────────────────────────────────────

    /// <summary>2026-08.zip이 있으면 2026-08_2.zip, 그것도 있으면 _3 ...</summary>
    private static string NextFreePath(string path)
    {
        string directory = Path.GetDirectoryName(path)!;
        string baseName = Path.GetFileNameWithoutExtension(path);

        for (int n = 2; ; n++)
        {
            string candidate = Path.Combine(directory, $"{baseName}_{n}.zip");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private static string NormalizeDirectory(string path)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 임시 파일 정리 실패는 무시한다. 다음 실행 때 다시 지워진다.
        }
    }
}
