namespace LogRogue.Core.Scanning;

/// <summary>
/// 대상 폴더 아래의 모든 파일을 훑어서, 수정한 날짜가 기간에 드는 파일을 날짜별로 묶는다.
///
/// 폴더 구조나 파일 이름은 보지 않는다. 파일마다 가진 수정한 날짜만으로 판정하므로
/// 날짜별 폴더에 나눠 쌓이는 로그든, 한 폴더에 계속 쌓이는 로그든 똑같이 다룬다.
///
/// 수정한 날짜가 오늘이거나 미래인 파일은 기간과 상관없이 항상 제외한다.
/// 오늘 수정됐다는 건 지금도 기록 중일 수 있다는 뜻이기 때문이다.
/// </summary>
public sealed class FileScanner
{
    private static readonly EnumerationOptions FileEnumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,   // 권한 없는 폴더는 건너뛴다

        // 바로 가기처럼 다른 곳을 가리키는 링크(심볼릭 링크, 정션)는 따라가지도, 대상으로 삼지도 않는다.
        // 링크를 따라가면 대상 폴더 밖의 파일을 지울 수 있다.
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    /// <param name="rootPath">대상 폴더.</param>
    /// <param name="start">시작일(포함).</param>
    /// <param name="end">종료일(포함).</param>
    /// <param name="today">오늘로 취급할 날짜. 생략하면 시스템 날짜를 쓴다.</param>
    public IReadOnlyList<LogDay> Scan(string rootPath, DateOnly start, DateOnly end, DateOnly? today = null)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("대상 폴더 경로가 비어 있습니다.", nameof(rootPath));

        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"대상 폴더를 찾을 수 없습니다: {rootPath}");

        if (start > end)
            (start, end) = (end, start);

        string root = Path.GetFullPath(rootPath);
        DateOnly cutoff = today ?? DateOnly.FromDateTime(DateTime.Today);

        var byDate = new Dictionary<DateOnly, List<LogFile>>();

        foreach (string path in Directory.EnumerateFiles(root, "*", FileEnumeration))
        {
            DateTime lastWrite;
            long length;

            try
            {
                var info = new FileInfo(path);
                lastWrite = info.LastWriteTime;
                length = info.Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;   // 훑는 사이에 지워졌거나 읽을 수 없는 파일
            }

            DateOnly date = DateOnly.FromDateTime(lastWrite);

            if (date >= cutoff)
                continue;   // 오늘 수정된 파일은 아직 기록 중일 수 있다

            if (date < start || date > end)
                continue;

            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');

            if (!byDate.TryGetValue(date, out List<LogFile>? files))
                byDate[date] = files = new List<LogFile>();

            files.Add(new LogFile(path, relative, length, lastWrite));
        }

        return byDate
            .OrderBy(pair => pair.Key)
            .Select(pair => new LogDay
            {
                Date = pair.Key,
                Files = pair.Value.OrderBy(f => f.RelativePath, StringComparer.Ordinal).ToList()
            })
            .ToList();
    }
}
