using LogRogue.Core.Archiving;
using LogRogue.Core.Scanning;

namespace LogRogue.Core;

/// <summary>
/// 백업 한 번의 전체 흐름을 실행한다.
///   1단계 Prepare: 경로 검증 → 기간에 해당하는 날짜 폴더 찾기 (아무것도 바꾸지 않음)
///   2단계 Run:     찾은 날짜 폴더들을 하나씩 압축
/// 두 단계 사이에 사용자에게 대상을 보여주고 확인받을 수 있다.
/// UI와 무관하게 동작하므로 나중에 스케줄러나 명령줄 실행에서도 그대로 쓸 수 있다.
/// </summary>
public sealed class BackupJob
{
    private readonly FolderScanner _scanner = new();
    private readonly DayFolderArchiver _archiver = new();

    /// <summary>
    /// 경로를 검증하고 압축 대상 날짜 폴더 목록을 돌려준다.
    /// 파일을 만들거나 지우지 않으므로 몇 번을 호출해도 안전하다.
    /// </summary>
    public IReadOnlyList<LogDayFolder> Prepare(
        string sourceRoot,
        string outputDirectory,
        DateOnly start,
        DateOnly end)
    {
        DayFolderArchiver.ValidatePaths(sourceRoot, outputDirectory);
        return _scanner.Scan(sourceRoot, start, end);
    }

    /// <summary>Prepare로 얻은 날짜 폴더들을 압축한다.</summary>
    /// <param name="progress">날짜마다 시작·완료 시점에 보고를 받을 곳. 없으면 null.</param>
    public IReadOnlyList<ArchiveResult> Run(
        IReadOnlyList<LogDayFolder> targets,
        string outputDirectory,
        IProgress<BackupProgress>? progress = null)
    {
        var results = new List<ArchiveResult>(targets.Count);

        for (int i = 0; i < targets.Count; i++)
        {
            LogDayFolder target = targets[i];
            progress?.Report(new BackupProgress(i + 1, targets.Count, target, null));

            ArchiveResult result = _archiver.Archive(target, outputDirectory);
            results.Add(result);

            progress?.Report(new BackupProgress(i + 1, targets.Count, target, result));
        }

        return results;
    }

    /// <summary>확인 절차 없이 한 번에 실행한다. 스케줄 실행 등 무인 동작용.</summary>
    public IReadOnlyList<ArchiveResult> Run(
        string sourceRoot,
        string outputDirectory,
        DateOnly start,
        DateOnly end,
        IProgress<BackupProgress>? progress = null)
    {
        IReadOnlyList<LogDayFolder> targets = Prepare(sourceRoot, outputDirectory, start, end);
        return Run(targets, outputDirectory, progress);
    }
}