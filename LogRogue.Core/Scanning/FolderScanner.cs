using System.Globalization;

namespace LogRogue.Core.Scanning;

/// <summary>
/// 대상 폴더 아래의 날짜 폴더(yyyy-MM-dd)를 찾아
/// 지정한 기간에 해당하는 것만 골라낸다.
/// </summary>
public sealed class FolderScanner
{
    /// <summary>날짜 폴더 이름 형식.</summary>
    public const string DateFolderFormat = "yyyy-MM-dd";

    private static readonly EnumerationOptions FileEnumeration = new()
    {
        RecurseSubdirectories = true,   // eventfile 같은 하위 폴더까지 센다
        IgnoreInaccessible = true,      // 권한 없는 항목은 건너뛴다
        AttributesToSkip = FileAttributes.None
    };

    /// <summary>
    /// 기간에 해당하는 날짜 폴더를 날짜순으로 반환한다.
    /// </summary>
    /// <param name="rootPath">날짜 폴더들이 들어 있는 대상 폴더.</param>
    /// <param name="start">시작일(포함).</param>
    /// <param name="end">종료일(포함).</param>
    /// <param name="today">
    /// 오늘로 취급할 날짜. 이 날짜와 그 이후 폴더는 장비가 기록 중일 수
    /// 있으므로 무조건 제외한다. 생략하면 시스템 날짜를 쓴다.
    /// </param>
    public IReadOnlyList<LogDayFolder> Scan(
        string rootPath,
        DateOnly start,
        DateOnly end,
        DateOnly? today = null)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("대상 폴더 경로가 비어 있습니다.", nameof(rootPath));

        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"대상 폴더를 찾을 수 없습니다: {rootPath}");

        // 사용자가 날짜를 거꾸로 넣었으면 알아서 바로잡는다
        if (start > end)
            (start, end) = (end, start);

        DateOnly cutoff = today ?? DateOnly.FromDateTime(DateTime.Today);

        var result = new List<LogDayFolder>();

        foreach (string directory in Directory.EnumerateDirectories(rootPath))
        {
            string folderName = Path.GetFileName(directory);

            // 날짜 형식이 아닌 폴더는 로그와 무관한 것으로 보고 조용히 넘어간다
            if (!DateOnly.TryParseExact(
                    folderName,
                    DateFolderFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateOnly date))
            {
                continue;
            }

            if (date < start || date > end)
                continue;

            // 오늘 및 미래 폴더는 파일이 열려 있을 수 있으므로 절대 대상에 넣지 않는다
            if (date >= cutoff)
                continue;

            (int fileCount, long totalBytes) = Measure(directory);

            result.Add(new LogDayFolder
            {
                Date = date,
                Path = directory,
                FileCount = fileCount,
                TotalBytes = totalBytes
            });
        }

        result.Sort((a, b) => a.Date.CompareTo(b.Date));
        return result;
    }

    /// <summary>폴더 하나의 파일 개수와 총 용량을 센다.</summary>
    private static (int FileCount, long TotalBytes) Measure(string directory)
    {
        int count = 0;
        long bytes = 0;

        foreach (string file in Directory.EnumerateFiles(directory, "*", FileEnumeration))
        {
            try
            {
                bytes += new FileInfo(file).Length;
                count++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 읽을 수 없는 파일은 집계에서 빠진다.
                // 나중에 이력 기록을 붙일 때 여기서 사유를 남기면 된다.
            }
        }

        return (count, bytes);
    }
}
