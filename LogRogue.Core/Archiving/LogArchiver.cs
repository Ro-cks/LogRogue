using System.IO.Compression;
using LogRogue.Core.Scanning;

namespace LogRogue.Core.Archiving;

/// <summary>
/// 날짜 묶음 하나를 ZIP 파일 하나로 압축한다.
/// 예: 수정한 날짜가 8/17 ~ 8/23인 파일들  →  D:\Backup\2026-08-17_2026-08-23.zip
///
/// 압축 파일 안에는 대상 폴더 기준의 원래 경로가 그대로 들어간다.
/// 풀면 대상 폴더 아래에 있던 모양 그대로 복원된다.
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
            int renamed = 0;
            bool savedSeparately = false;

            // using: 쓰기가 끝나면 기존 압축 파일을 닫는다.
            // 열려 있는 상태로는 아래 File.Move가 그 파일을 교체할 수 없다.
            using (ZipArchive? existing = TryOpenExisting(finalPath, out bool existedButUnreadable))
            {
                if (existing is not null)
                {
                    (carriedOver, renamed) = MergeExisting(existing, contents);
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
                RenamedEntries = renamed,
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
    /// FilePath가 있으면 디스크의 원본 파일이고,
    /// null이면 기존 압축 파일 안의 ExistingName 항목을 옮겨 담는다.
    /// </summary>
    private sealed record EntrySource(string? FilePath, long Length, DateTime LastWriteTime, string? ExistingName);

    /// <summary>
    /// 묶음 안의 모든 파일을 "압축 파일 내부 경로 → 원본"으로 정리한다.
    /// 내부 경로는 대상 폴더 기준 상대 경로 그대로다.
    ///   예) 대상 폴더\2026-08-20\globalfile\a.log  →  2026-08-20/globalfile/a.log
    ///       대상 폴더\app_20260820.log             →  app_20260820.log
    /// </summary>
    private static Dictionary<string, EntrySource> CollectFiles(ArchiveGroup group)
    {
        var files = new Dictionary<string, EntrySource>(StringComparer.Ordinal);

        foreach (LogFile file in group.Days.SelectMany(d => d.Files))
            files.Add(file.RelativePath, new EntrySource(file.FullPath, file.Length, file.LastWriteTime, null));

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
    /// 기존 압축 파일의 항목들을 새로 압축할 목록에 합친다.
    /// 옮겨 담은 항목 수와, 그중 이름을 바꿔 보존한 항목 수를 돌려준다.
    ///
    /// 기존에만 있는 항목
    ///   원본이 이미 삭제된 파일이다. 그대로 옮겨 담는다.
    ///
    /// 같은 경로가 양쪽에 있고 수정한 시각도 같은 항목
    ///   같은 파일이다. 더 큰 쪽을 남긴다. 로그는 뒤에 덧붙여지기만 하므로 큰 쪽이 더 완전하다.
    ///
    /// 같은 경로가 양쪽에 있지만 수정한 시각이 다른 항목
    ///   이름만 같은 다른 파일이다. 매일 같은 이름으로 새로 만들어지는 로그(app.log 등)가 그렇다.
    ///   둘 다 남긴다. 디스크의 파일은 원래 이름으로, 기존 것은 이름 뒤에 시각을 붙여서.
    ///     app.log  →  app (2026-08-20 153012).log
    /// </summary>
    private static (int CarriedOver, int Renamed) MergeExisting(ZipArchive existing, Dictionary<string, EntrySource> contents)
    {
        var oldEntries = existing.Entries.Where(e => e.Name.Length > 0).ToList();

        // 이름을 바꿀 때 기존 압축 파일 안의 다른 항목과 겹치지 않도록 미리 모든 이름을 알아둔다
        var reservedNames = new HashSet<string>(oldEntries.Select(e => e.FullName), StringComparer.Ordinal);

        int carried = 0;
        int renamed = 0;

        foreach (ZipArchiveEntry entry in oldEntries)
        {
            var fromExisting = new EntrySource(null, entry.Length, entry.LastWriteTime.DateTime, entry.FullName);

            if (!contents.TryGetValue(entry.FullName, out EntrySource? current))
            {
                contents[entry.FullName] = fromExisting;
                carried++;
                continue;
            }

            if (IsSameFile(current, entry))
            {
                if (entry.Length > current.Length)
                {
                    contents[entry.FullName] = fromExisting;
                    carried++;
                }

                continue;
            }

            string preservedName = UniqueName(DatedName(entry.FullName, entry.LastWriteTime.DateTime), contents, reservedNames);
            contents[preservedName] = fromExisting;
            reservedNames.Add(preservedName);
            carried++;
            renamed++;
        }

        return (carried, renamed);
    }

    /// <summary>
    /// 디스크의 파일과 압축 파일 안의 항목이 같은 파일인지. 수정한 시각으로 판단한다.
    /// ZIP은 시각을 2초 단위로 저장하므로 2초까지의 차이는 같은 것으로 본다.
    /// </summary>
    private static bool IsSameFile(EntrySource onDisk, ZipArchiveEntry inZip)
        => Math.Abs((ZipTime(onDisk.LastWriteTime) - inZip.LastWriteTime.DateTime).TotalSeconds) <= 2;

    /// <summary>ZIP이 저장할 수 있는 시각 범위(1980~2107년)로 맞춘다. 범위를 벗어난 시각은 끝값으로 저장되기 때문이다.</summary>
    private static DateTime ZipTime(DateTime time)
    {
        var min = new DateTime(1980, 1, 1);
        var max = new DateTime(2107, 12, 31, 23, 59, 58);
        return time < min ? min : time > max ? max : time;
    }

    /// <summary>logs/app.log → logs/app (2026-08-20 153012).log</summary>
    private static string DatedName(string entryName, DateTime lastWrite)
    {
        int slash = entryName.LastIndexOf('/');
        string folder = slash >= 0 ? entryName[..(slash + 1)] : "";
        string file = slash >= 0 ? entryName[(slash + 1)..] : entryName;

        string extension = Path.GetExtension(file);
        string stem = file[..^extension.Length];

        return $"{folder}{stem} ({lastWrite:yyyy-MM-dd HHmmss}){extension}";
    }

    private static string UniqueName(string candidate, Dictionary<string, EntrySource> contents, HashSet<string> reserved)
    {
        if (!contents.ContainsKey(candidate) && !reserved.Contains(candidate))
            return candidate;

        string extension = Path.GetExtension(candidate);
        string stem = candidate[..^extension.Length];

        for (int n = 2; ; n++)
        {
            string next = $"{stem} {n}{extension}";
            if (!contents.ContainsKey(next) && !reserved.Contains(next))
                return next;
        }
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

            // 기존 압축 파일에서 풀어서 새 압축 파일로 다시 담는다. 이름이 바뀌었을 수 있다.
            ZipArchiveEntry oldEntry = existing!.GetEntry(source.ExistingName!)
                ?? throw new InvalidDataException($"기존 압축 파일에서 항목을 찾지 못했습니다: {source.ExistingName}");

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
