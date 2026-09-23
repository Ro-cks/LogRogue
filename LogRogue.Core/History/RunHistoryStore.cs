using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogRogue.Core.History;

/// <summary>
/// 작업 이력을 파일에 저장하고 읽는다.
/// 한 줄에 실행 한 건(JSON)이라 새 기록을 덧붙이기만 하면 되고,
/// 한 줄이 깨져도 나머지 줄은 그대로 읽힌다.
/// </summary>
public sealed class RunHistoryStore
{
    /// <summary>보관할 최대 건수. 넘으면 오래된 것부터 버린다.</summary>
    public const int MaxEntries = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,   // 한 건이 한 줄이어야 한다
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private readonly object _gate = new();

    public RunHistoryStore() : this(AppPaths.HistoryFile) { }

    public RunHistoryStore(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    /// <summary>이력 한 건을 덧붙인다. 실패하면 예외를 던진다.</summary>
    public void Append(RunHistoryEntry entry)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine, Utf8NoBom);
            TrimIfTooLong();
        }
    }

    /// <summary>최근 기록부터 읽는다. 읽지 못하면 빈 목록을 돌려준다.</summary>
    public IReadOnlyList<RunHistoryEntry> LoadRecent(int max = MaxEntries)
    {
        lock (_gate)
        {
            var entries = new List<RunHistoryEntry>();

            foreach (string line in ReadLines())
            {
                if (TryParse(line) is RunHistoryEntry entry)
                    entries.Add(entry);
            }

            entries.Reverse();   // 최근 것이 위로
            return entries.Take(max).ToList();
        }
    }

    private string[] ReadLines()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllLines(FilePath, Encoding.UTF8) : Array.Empty<string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static RunHistoryEntry? TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        try
        {
            return JsonSerializer.Deserialize<RunHistoryEntry>(line, JsonOptions);
        }
        catch (JsonException)
        {
            return null;   // 깨진 줄은 건너뛴다
        }
    }

    /// <summary>건수가 상한을 넘으면 최근 것만 남기고 다시 쓴다.</summary>
    private void TrimIfTooLong()
    {
        string[] lines = ReadLines();
        if (lines.Length <= MaxEntries)
            return;

        string[] kept = lines[^MaxEntries..];
        string tempPath = FilePath + ".tmp";

        File.WriteAllLines(tempPath, kept, Utf8NoBom);
        File.Move(tempPath, FilePath, overwrite: true);
    }
}
