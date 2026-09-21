using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.Scanning;

namespace LogRogue.Core;

/// <summary>
/// 백업 한 번의 전체 흐름을 실행한다.
///   1단계 Prepare: 경로 검증 → 기간에 해당하는 날짜 폴더 찾기 (아무것도 바꾸지 않음)
///   2단계 Run:     날짜마다 압축 → 검증 → (선택) 원본 삭제
/// 두 단계 사이에 사용자에게 대상을 보여주고 확인받을 수 있다.
/// UI와 무관하게 동작하므로 나중에 스케줄러나 명령줄 실행에서도 그대로 쓸 수 있다.
/// </summary>
public sealed class BackupJob
{
    private readonly FolderScanner _scanner = new();
    private readonly DayFolderArchiver _archiver = new();
    private readonly SourceDeleter _deleter = new();

    /// <summary>
    /// 경로를 검증하고 압축 대상 날짜 폴더를 찾아 계획으로 돌려준다.
    /// 파일을 만들거나 지우지 않으므로 몇 번을 호출해도 안전하다.
    /// </summary>
    public BackupPlan Prepare(string sourceRoot, string outputDirectory, DateOnly start, DateOnly end)
    {
        DayFolderArchiver.ValidatePaths(sourceRoot, outputDirectory);

        string fullSource = Path.GetFullPath(sourceRoot);
        string fullOutput = Path.GetFullPath(outputDirectory);

        return new BackupPlan
        {
            SourceRoot = fullSource,
            OutputDirectory = fullOutput,
            Targets = _scanner.Scan(fullSource, start, end)
        };
    }

    /// <summary>
    /// 계획대로 날짜마다 압축하고, 요청 시 원본을 삭제한다.
    /// 삭제는 그 날짜의 압축과 검증이 성공한 직후에만 수행한다.
    /// 한 날짜씩 끝내고 넘어가므로 도중에 멈춰도 이미 지워진 날짜는 모두 압축본이 있다.
    /// </summary>
    public IReadOnlyList<DayBackupResult> Run(
        BackupPlan plan,
        DeleteMode deleteMode,
        IProgress<BackupProgress>? progress = null)
    {
        var results = new List<DayBackupResult>(plan.Targets.Count);

        for (int i = 0; i < plan.Targets.Count; i++)
        {
            LogDayFolder target = plan.Targets[i];
            progress?.Report(new BackupProgress(i + 1, plan.Targets.Count, target, null));

            ArchiveResult archive = _archiver.Archive(target, plan.OutputDirectory);

            DeletionResult? deletion = null;
            if (deleteMode != DeleteMode.None && archive.Succeeded)
                deletion = _deleter.Delete(archive, plan.SourceRoot, deleteMode);

            var result = new DayBackupResult { Archive = archive, Deletion = deletion };
            results.Add(result);

            progress?.Report(new BackupProgress(i + 1, plan.Targets.Count, target, result));
        }

        return results;
    }
}