using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.Diagnostics;
using LogRogue.Core.Scanning;

namespace LogRogue.Core;

/// <summary>
/// 백업 한 번의 전체 흐름을 실행한다.
///   1단계 Prepare: 경로 검증 → 기간에 해당하는 날짜 폴더 찾기 → 압축 단위로 묶기 (아무것도 바꾸지 않음)
///   2단계 Run:     묶음마다 압축 → 검증 → (선택) 그 묶음의 원본 날짜 폴더 삭제
/// 두 단계 사이에 사용자에게 대상을 보여주고 확인받을 수 있다.
/// UI와 무관하게 동작하므로 나중에 스케줄러나 명령줄 실행에서도 그대로 쓸 수 있다.
/// </summary>
public sealed class BackupJob
{
    private readonly FolderScanner _scanner = new();
    private readonly LogArchiver _archiver = new();
    private readonly SourceDeleter _deleter = new();
    private readonly IAppLog _log;

    public BackupJob(IAppLog? log = null) => _log = log ?? NullLog.Instance;

    /// <summary>
    /// 경로를 검증하고 압축 대상 날짜 폴더를 찾아 묶은 계획을 돌려준다.
    /// 파일을 만들거나 지우지 않으므로 몇 번을 호출해도 안전하다.
    /// </summary>
    /// <param name="today">오늘로 취급할 날짜. 생략하면 시스템 날짜를 쓴다. (테스트용)</param>
    public BackupPlan Prepare(
        string sourceRoot,
        string outputDirectory,
        BackupPeriod period,
        ArchiveGrouping grouping,
        DateOnly? today = null)
    {
        LogArchiver.ValidatePaths(sourceRoot, outputDirectory);

        string fullSource = Path.GetFullPath(sourceRoot);
        string fullOutput = Path.GetFullPath(outputDirectory);

        // 상대 기간은 여기서 실행 시점 기준으로 계산된다.
        // 예약 실행에서도 같은 설정으로 매번 알맞은 날짜 범위가 나온다.
        (DateOnly start, DateOnly end) = period.Resolve(today);

        IReadOnlyList<LogDayFolder> days = _scanner.Scan(fullSource, start, end, today);

        return new BackupPlan
        {
            SourceRoot = fullSource,
            OutputDirectory = fullOutput,
            Period = period,
            Grouping = grouping,
            Days = days,
            Groups = ArchiveGrouper.Group(days, grouping)
        };
    }

    /// <summary>
    /// 계획대로 묶음마다 압축하고, 요청 시 원본을 삭제한다.
    /// 삭제는 그 묶음의 압축과 검증이 성공한 직후에만 수행한다.
    /// 한 묶음씩 끝내고 넘어가므로 도중에 멈춰도 이미 지워진 날짜는 모두 압축본이 있다.
    /// </summary>
    public IReadOnlyList<GroupBackupResult> Run(
        BackupPlan plan,
        DeleteMode deleteMode,
        IProgress<BackupProgress>? progress = null)
    {
        _log.Info(
            $"백업 시작: {plan.Period.Describe()} · 대상 {plan.Days.Count}일 · 압축 파일 {plan.Groups.Count}개 · " +
            $"단위 {plan.Grouping} · 삭제 {deleteMode} · {plan.SourceRoot} → {plan.OutputDirectory}");

        var results = new List<GroupBackupResult>(plan.Groups.Count);

        for (int i = 0; i < plan.Groups.Count; i++)
        {
            ArchiveGroup group = plan.Groups[i];
            progress?.Report(new BackupProgress(i + 1, plan.Groups.Count, group, null));

            ArchiveResult archive = _archiver.Archive(group, plan.OutputDirectory);

            if (archive.Succeeded)
            {
                _log.Info(
                    $"압축 완료 {group.FileName}: {group.Days.Count}일 {group.FileCount}개 파일 · " +
                    $"{ByteSize.ToDisplay(group.TotalBytes)} → {ByteSize.ToDisplay(archive.ArchiveBytes)}" +
                    (archive.CarriedOverEntries > 0 ? $" · 기존 항목 {archive.CarriedOverEntries}개 합침" : "") +
                    (archive.SavedSeparately ? $" · 기존 파일을 읽지 못해 {Path.GetFileName(archive.ArchivePath)}(으)로 저장" : ""));
            }
            else
            {
                _log.Error($"압축 실패 {group.FileName}: {archive.Error}");
            }

            var deletions = new List<DayDeletion>();
            if (deleteMode != DeleteMode.None && archive.Succeeded)
            {
                foreach (LogDayFolder day in group.Days)
                {
                    DeletionResult deletion = _deleter.Delete(day, archive, plan.SourceRoot, deleteMode);
                    deletions.Add(new DayDeletion(day, deletion));

                    if (!deletion.Succeeded)
                        _log.Warn($"원본 삭제 실패 {day.Date:yyyy-MM-dd}: {deletion.Error}");
                }
            }

            var result = new GroupBackupResult { Archive = archive, Deletions = deletions };
            results.Add(result);

            progress?.Report(new BackupProgress(i + 1, plan.Groups.Count, group, result));
        }

        int deleted = results.Sum(r => r.DeletedDays);
        _log.Info(
            $"백업 종료: 압축 성공 {results.Count(r => r.Archive.Succeeded)}개 / 실패 {results.Count(r => !r.Archive.Succeeded)}개" +
            (deleteMode != DeleteMode.None ? $" · 원본 삭제 {deleted}일" : ""));

        return results;
    }
}
