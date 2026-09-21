using System.IO.Compression;
using LogRogue.Core.Scanning;

namespace LogRogue.Core.Archiving;

/// <summary>
/// 날짜 폴더 하나를 ZIP 파일 하나로 압축한다.
/// 예: C:\Logs\2026-08-20  →  D:\Backup\2026-08-20.zip
///
/// 처리 순서
///   1. 폴더 안의 모든 파일 목록과 크기를 기록
///   2. 임시 파일(.zip.tmp)에 압축
///   3. 임시 파일을 다시 열어 원본과 대조 검증
///   4. 검증 통과 시에만 최종 이름(.zip)으로 변경
/// 어느 단계에서든 실패하면 임시 파일을 지우고 실패 결과를 돌려준다.
/// 출력 폴더에 깨진 .zip 파일이 남는 일은 없다.
/// </summary>
public sealed class DayFolderArchiver
{
    private const string TempExtension = ".tmp";

    // 스캔 때와 달리 권한 없는 항목을 건너뛰지 않는다.
    // 압축 후 원본을 지울 수도 있으므로, 하나라도 못 읽으면 그 날짜 전체를 실패로 처리해야 안전하다.
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
    /// 날짜 폴더가 압축될 파일 경로를 돌려준다. 예: D:\Backup\2026-08-20.zip
    /// 압축 파일 이름 규칙은 이 메서드 한 곳에서만 정한다.
    /// </summary>
    public static string GetArchivePath(LogDayFolder folder, string outputDirectory)
        => Path.Combine(outputDirectory, Path.GetFileName(folder.Path) + ".zip");

    /// <summary>날짜 폴더 하나를 압축한다. 예외를 던지지 않고 항상 결과를 돌려준다.</summary>
    public ArchiveResult Archive(LogDayFolder folder, string outputDirectory)
    {
        string dateName = Path.GetFileName(folder.Path);
        string finalPath = GetArchivePath(folder, outputDirectory);
        string tempPath = finalPath + TempExtension;

        try
        {
            Directory.CreateDirectory(outputDirectory);

            Dictionary<string, SourceFile> expected = CollectFiles(folder.Path, dateName);
            if (expected.Count == 0)
                return ArchiveResult.Failure(folder, "압축할 파일이 없습니다.");

            DeleteQuietly(tempPath);   // 이전에 비정상 종료로 남은 임시 파일 정리

            WriteArchive(tempPath, expected);
            VerifyArchive(tempPath, expected);

            // 검증을 통과한 뒤에야 최종 파일로 바꾼다.
            // 같은 이름의 압축 파일이 이미 있으면 새로 검증된 것으로 교체한다.
            // 원본 폴더가 아직 남아 있다면 그 내용이 가장 최신이기 때문이다.
            File.Move(tempPath, finalPath, overwrite: true);

            return ArchiveResult.Success(folder, finalPath, new FileInfo(finalPath).Length);
        }
        catch (Exception ex)
        {
            // 한 날짜가 실패해도 나머지 날짜는 계속 처리할 수 있도록
            // 예외를 결과로 바꿔서 돌려준다.
            DeleteQuietly(tempPath);
            return ArchiveResult.Failure(folder, ex.Message);
        }
    }

    /// <summary>
    /// 폴더 안의 모든 파일을 압축 파일 내부 경로 → 원본 정보로 정리한다.
    /// 내부 경로는 날짜 폴더명부터 시작한다. 예: 2026-08-20/globalfile/2026-08-20_global.log
    /// 이렇게 해두면 압축을 풀었을 때 날짜 폴더가 그대로 다시 만들어진다.
    /// </summary>
    private static Dictionary<string, SourceFile> CollectFiles(string folderPath, string dateName)
    {
        var files = new Dictionary<string, SourceFile>(StringComparer.Ordinal);

        foreach (string fullPath in Directory.EnumerateFiles(folderPath, "*", FileEnumeration))
        {
            string relative = Path.GetRelativePath(folderPath, fullPath).Replace('\\', '/');
            string entryName = $"{dateName}/{relative}";

            files.Add(entryName, new SourceFile(fullPath, new FileInfo(fullPath).Length));
        }

        return files;
    }

    private void WriteArchive(string zipPath, Dictionary<string, SourceFile> files)
    {
        // using 블록을 벗어날 때 ZipArchive가 닫히면서 파일 끝의 목차가 기록된다.
        // 반드시 검증보다 먼저 닫혀 있어야 한다.
        using var stream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

        foreach (var (entryName, source) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
            zip.CreateEntryFromFile(source.FullPath, entryName, Level);
    }

    /// <summary>
    /// 만들어진 압축 파일을 다시 열어 원본과 하나하나 대조한다.
    /// 파일 개수, 이름, 그리고 실제로 끝까지 풀었을 때의 크기가 모두 일치해야 통과한다.
    /// </summary>
    private static void VerifyArchive(string zipPath, Dictionary<string, SourceFile> expected)
    {
        using ZipArchive zip = ZipFile.OpenRead(zipPath);

        // 이름이 비어 있는 항목은 폴더 항목이므로 제외
        var fileEntries = zip.Entries.Where(e => e.Name.Length > 0).ToList();

        if (fileEntries.Count != expected.Count)
            throw new InvalidDataException(
                $"검증 실패: 파일 개수 불일치 (원본 {expected.Count}개, 압축본 {fileEntries.Count}개)");

        foreach (ZipArchiveEntry entry in fileEntries)
        {
            if (!expected.TryGetValue(entry.FullName, out SourceFile? source))
                throw new InvalidDataException($"검증 실패: 원본에 없는 항목 {entry.FullName}");

            long actual = CountDecompressedBytes(entry);

            if (actual != source.Length)
                throw new InvalidDataException(
                    $"검증 실패: 크기 불일치 {entry.FullName} (원본 {source.Length}, 압축 해제 {actual})");
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

    private sealed record SourceFile(string FullPath, long Length);
}