using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.Jobs;
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
        settings.Jobs ??= new List<JobConfig>();

        MigrateVersion1(settings);

        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (JobConfig job in settings.Jobs)
        {
            NormalizeJob(job);

            // 식별자나 이름이 겹치면 이력이 섞이거나 목록에서 구분되지 않으므로 바로잡는다
            if (string.IsNullOrWhiteSpace(job.Id) || !usedIds.Add(job.Id))
            {
                job.Id = JobConfig.NewId();
                usedIds.Add(job.Id);
            }

            string baseName = string.IsNullOrWhiteSpace(job.Name) ? "작업" : job.Name.Trim();
            string name = baseName;
            for (int n = 2; !usedNames.Add(name); n++)
                name = $"{baseName} {n}";
            job.Name = name;
        }

        settings.Version = AppSettings.CurrentVersion;
    }

    /// <summary>
    /// 작업이 하나뿐이던 예전 설정을 "기본 작업"으로 옮긴다.
    /// 식별자를 고정값으로 두어 예전 작업 이력과 이어지게 한다.
    /// 예약의 마지막 실행 시각도 그대로 옮겨서 예약이 끊기지 않는다.
    /// </summary>
    private static void MigrateVersion1(AppSettings settings)
    {
        bool hasVersion1 = settings.SourcePath is not null || settings.Schedule is not null;

        if (hasVersion1 && settings.Jobs.Count == 0)
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Today);

            settings.Jobs.Add(new JobConfig
            {
                Id = JobConfig.LegacyId,
                Name = "기본 작업",
                SourcePath = settings.SourcePath ?? "",
                OutputPath = settings.OutputPath ?? "",
                PeriodMode = settings.PeriodMode ?? PeriodMode.Absolute,
                StartDate = today.AddDays(-30),
                EndDate = today.AddDays(-1),
                KeepRecentDays = settings.KeepRecentDays ?? 7,
                Grouping = settings.Grouping ?? ArchiveGrouping.Daily,
                DeleteSource = settings.DeleteSource ?? false,
                DeleteMode = settings.DeleteMode ?? Deletion.DeleteMode.RecycleBin,
                Schedule = settings.Schedule ?? new ScheduleSettings()
            });
        }

        settings.SourcePath = null;
        settings.OutputPath = null;
        settings.DeleteSource = null;
        settings.DeleteMode = null;
        settings.Grouping = null;
        settings.PeriodMode = null;
        settings.KeepRecentDays = null;
        settings.Schedule = null;
    }

    private static void NormalizeJob(JobConfig job)
    {
        job.Name ??= "";
        job.SourcePath ??= "";
        job.OutputPath ??= "";

        if (job.DeleteMode is not (DeleteMode.RecycleBin or DeleteMode.Permanent))
            job.DeleteMode = DeleteMode.RecycleBin;

        if (!Enum.IsDefined(job.Grouping))
            job.Grouping = ArchiveGrouping.Daily;

        if (!Enum.IsDefined(job.PeriodMode))
            job.PeriodMode = PeriodMode.Relative;

        job.KeepRecentDays = BackupPeriod.Clamp(job.KeepRecentDays);

        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        if (job.StartDate == default)
            job.StartDate = today.AddDays(-30);
        if (job.EndDate == default)
            job.EndDate = today.AddDays(-1);

        job.Schedule ??= new ScheduleSettings();
        ScheduleSettings schedule = job.Schedule;

        if (!Enum.IsDefined(schedule.Mode))
            schedule.Mode = ScheduleMode.Daily;

        schedule.IntervalHours = Math.Clamp(
            schedule.IntervalHours, ScheduleSettings.MinIntervalHours, ScheduleSettings.MaxIntervalHours);

        // 요일이 중복되거나 비어 있으면 바로잡는다. 하나도 없으면 매주 방식이 영영 실행되지 않는다.
        schedule.Weekdays = (schedule.Weekdays ?? new List<DayOfWeek>())
            .Where(Enum.IsDefined)
            .Distinct()
            .ToList();

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
