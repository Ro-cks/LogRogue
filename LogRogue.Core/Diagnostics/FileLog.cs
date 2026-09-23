using System.Globalization;
using System.Text;

namespace LogRogue.Core.Diagnostics;

/// <summary>
/// 날짜별 파일에 기록하는 로그.
///   logrogue-20260923.log
///   logrogue-20260923_2.log   (하루치가 용량 상한을 넘으면 이어서 만든다)
///
/// 백업 도구가 스스로 용량을 잡아먹으면 본말전도이므로
/// 파일 하나의 크기와 보관 일수를 모두 제한한다.
/// 기록에 실패해도 예외를 밖으로 내보내지 않는다. 로그 때문에 백업이 멈추면 안 된다.
/// </summary>
public sealed class FileLog : IAppLog
{
    private const string FilePrefix = "logrogue-";
    private const string FileExtension = ".log";
    private const string DateFormat = "yyyyMMdd";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly object _gate = new();
    private readonly string _directory;
    private readonly int _retentionDays;
    private readonly long _maxBytesPerFile;

    private DateOnly _lastCleanupDate = DateOnly.MinValue;

    /// <param name="directory">로그 파일을 둘 폴더.</param>
    /// <param name="retentionDays">보관 일수. 이보다 오래된 로그 파일은 지운다.</param>
    /// <param name="maxBytesPerFile">파일 하나의 최대 크기.</param>
    public FileLog(string directory, int retentionDays = 14, long maxBytesPerFile = 5 * 1024 * 1024)
    {
        _directory = directory;
        _retentionDays = Math.Max(1, retentionDays);
        _maxBytesPerFile = Math.Max(64 * 1024, maxBytesPerFile);
    }

    public void Info(string message) => Write("INFO", message, null);
    public void Warn(string message) => Write("WARN", message, null);
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    /// <summary>지금 기록되고 있는 파일 경로. 로그 폴더를 열어줄 때 쓴다.</summary>
    public string Directory => _directory;

    private void Write(string level, string message, Exception? exception)
    {
        DateTime now = DateTime.Now;

        var line = new StringBuilder()
            .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(" [").Append(level).Append("] ")
            .Append(message);

        if (exception is not null)
            line.Append(Environment.NewLine).Append("    ").Append(exception.GetType().Name).Append(": ").Append(exception.Message);

        line.Append(Environment.NewLine);

        // 여러 스레드가 동시에 기록해도 줄이 섞이지 않도록 한 번에 하나씩만 쓴다
        lock (_gate)
        {
            try
            {
                System.IO.Directory.CreateDirectory(_directory);
                File.AppendAllText(ResolveFilePath(now), line.ToString(), Utf8NoBom);
                CleanupOldFiles(DateOnly.FromDateTime(now));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 로그를 남기지 못하는 것만으로 프로그램이 멈추지는 않는다
            }
        }
    }

    /// <summary>오늘 쓸 파일. 상한을 넘었으면 _2, _3 ... 으로 넘어간다.</summary>
    private string ResolveFilePath(DateTime now)
    {
        string basePath = Path.Combine(_directory, $"{FilePrefix}{now.ToString(DateFormat)}{FileExtension}");

        var info = new FileInfo(basePath);
        if (!info.Exists || info.Length < _maxBytesPerFile)
            return basePath;

        for (int part = 2; ; part++)
        {
            string candidate = Path.Combine(
                _directory, $"{FilePrefix}{now.ToString(DateFormat)}_{part}{FileExtension}");

            var partInfo = new FileInfo(candidate);
            if (!partInfo.Exists || partInfo.Length < _maxBytesPerFile)
                return candidate;
        }
    }

    /// <summary>보관 기간이 지난 로그 파일을 지운다. 하루에 한 번만 확인한다.</summary>
    private void CleanupOldFiles(DateOnly today)
    {
        if (_lastCleanupDate == today)
            return;

        _lastCleanupDate = today;
        DateOnly oldest = today.AddDays(-_retentionDays);

        foreach (string path in System.IO.Directory.EnumerateFiles(_directory, $"{FilePrefix}*{FileExtension}"))
        {
            if (TryParseDate(path) is DateOnly date && date < oldest)
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 지우지 못하면 다음 날 다시 시도한다
                }
            }
        }
    }

    /// <summary>파일 이름에서 날짜를 읽는다. logrogue-20260923_2.log → 2026-09-23</summary>
    private static DateOnly? TryParseDate(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (!name.StartsWith(FilePrefix, StringComparison.Ordinal))
            return null;

        string datePart = name[FilePrefix.Length..];
        int underscore = datePart.IndexOf('_');
        if (underscore >= 0)
            datePart = datePart[..underscore];

        return DateOnly.TryParseExact(datePart, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date
            : null;
    }
}
