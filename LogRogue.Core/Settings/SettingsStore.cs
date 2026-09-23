using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.Scheduling;

namespace LogRogue.Core.Settings;

/// <summary>불러온 설정과, 문제가 있었다면 사용자에게 알릴 메시지.</summary>
public sealed record SettingsLoadResult(AppSettings Settings, string? Warning);

/// <summary>
/// 설정을 JSON 파일로 저장하고 불러온다.
/// 기본 위치: %APPDATA%\LogRogue\settings.json
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,   // 사람이 메모장으로 열어봐도 읽기 쉽게

        // 기본 설정은 한글을 \uD55C 같은 코드로 바꿔 저장한다.
        // 로컬 설정 파일이라 그대로 저장해도 문제없으므로 경로가 읽히도록 한다.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

        // 삭제 방식을 숫자(1, 2) 대신 이름("RecycleBin")으로 저장한다
        Converters = { new JsonStringEnumConverter() }
    };

    public SettingsStore() : this(DefaultFilePath) { }

    /// <param name="filePath">설정 파일 경로. 테스트할 때 임시 경로를 넘길 수 있다.</param>
    public SettingsStore(string filePath)
    {
        FilePath = filePath;
    }

    public static string DefaultFilePath => AppPaths.SettingsFile;

    public string FilePath { get; }

    /// <summary>
    /// 설정을 불러온다. 예외를 던지지 않는다.
    /// 파일이 없으면 기본값을, 파일이 깨졌으면 백업해두고 기본값을 돌려준다.
    /// </summary>
    public SettingsLoadResult Load()
    {
        if (!File.Exists(FilePath))
            return new SettingsLoadResult(new AppSettings(), null);

        try
        {
            string json = File.ReadAllText(FilePath, Encoding.UTF8);
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                ?? throw new JsonException("설정 파일 내용이 비어 있습니다.");

            Normalize(settings);
            return new SettingsLoadResult(settings, null);
        }
        catch (JsonException)
        {
            // 깨진 파일은 지우지 않고 이름을 바꿔 남겨둔다. 수동으로 살펴볼 수 있도록.
            string? backup = BackupCorruptFile();
            string where = backup is null ? "" : $" 기존 파일은 {Path.GetFileName(backup)}(으)로 보관했습니다.";
            return new SettingsLoadResult(new AppSettings(), $"설정 파일이 손상되어 기본값으로 시작합니다.{where}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new SettingsLoadResult(new AppSettings(), $"설정 파일을 읽지 못해 기본값으로 시작합니다: {ex.Message}");
        }
    }

    /// <summary>
    /// 설정을 저장한다. 실패하면 예외를 던진다.
    /// 임시 파일에 먼저 쓰고 바꿔치기하므로, 저장 도중 꺼져도 기존 설정 파일이 깨지지 않는다.
    /// </summary>
    public void Save(AppSettings settings)
    {
        settings.Version = AppSettings.CurrentVersion;

        string directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);

        string tempPath = FilePath + ".tmp";
        string json = JsonSerializer.Serialize(settings, JsonOptions);

        File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tempPath, FilePath, overwrite: true);
    }

    /// <summary>파일을 손으로 고쳤거나 옛 형식일 때 이상한 값을 바로잡는다.</summary>
    private static void Normalize(AppSettings settings)
    {
        settings.SourcePath ??= "";
        settings.OutputPath ??= "";

        // 설정 화면에서 고를 수 있는 건 휴지통과 영구 삭제 둘뿐이다
        if (settings.DeleteMode is not (DeleteMode.RecycleBin or DeleteMode.Permanent))
            settings.DeleteMode = DeleteMode.RecycleBin;

        if (!Enum.IsDefined(settings.Grouping))
            settings.Grouping = ArchiveGrouping.Daily;

        if (!Enum.IsDefined(settings.PeriodMode))
            settings.PeriodMode = PeriodMode.Absolute;

        settings.KeepRecentDays = BackupPeriod.Clamp(settings.KeepRecentDays);

        settings.Schedule ??= new ScheduleSettings();
        ScheduleSettings schedule = settings.Schedule;

        if (!Enum.IsDefined(schedule.Mode))
            schedule.Mode = ScheduleMode.Daily;

        schedule.IntervalHours = Math.Clamp(
            schedule.IntervalHours, ScheduleSettings.MinIntervalHours, ScheduleSettings.MaxIntervalHours);

        // 요일이 중복되거나 비어 있으면 바로잡는다. 하나도 없으면 매주 방식이 영영 실행되지 않는다.
        schedule.Weekdays = schedule.Weekdays is { Count: > 0 }
            ? schedule.Weekdays.Where(Enum.IsDefined).Distinct().ToList()
            : new List<DayOfWeek> { DayOfWeek.Monday };

        if (schedule.Weekdays.Count == 0)
            schedule.Weekdays.Add(DayOfWeek.Monday);
    }

    private string? BackupCorruptFile()
    {
        try
        {
            string backupPath = $"{FilePath}.broken-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(FilePath, backupPath, overwrite: true);
            return backupPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
