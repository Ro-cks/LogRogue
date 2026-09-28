using LogRogue.Core.Archiving;
using LogRogue.Core.Safety;

namespace LogRogue.Core.Jobs;

/// <summary>
/// 작업 설정이 올바른지, 그리고 다른 작업과 서로 해치지 않는지 확인한다.
///
/// 작업끼리 막아야 하는 조합
///   - 대상 폴더가 서로 겹침: 같은 파일을 두 작업이 함께 압축하고 지운다
///   - 출력 폴더가 같음: 2026-08.zip 같은 이름이 서로 겹친다
///   - 한 작업의 출력 폴더가 다른 작업의 대상 폴더 안에 있음:
///     한쪽이 만든 압축 파일을 다른 쪽이 로그로 알고 압축하고 지운다
/// </summary>
public static class JobValidator
{
    /// <summary>문제 목록을 돌려준다. 비어 있으면 통과.</summary>
    /// <param name="job">확인할 작업.</param>
    /// <param name="allJobs">전체 작업. 같은 Id를 가진 작업(자기 자신)은 비교에서 뺀다.</param>
    public static IReadOnlyList<string> Validate(JobConfig job, IEnumerable<JobConfig> allJobs)
    {
        var errors = new List<string>();
        List<JobConfig> others = allJobs.Where(o => o.Id != job.Id).ToList();

        string name = job.Name.Trim();
        if (name.Length == 0)
            errors.Add("작업 이름을 입력하세요.");
        else if (others.Any(o => string.Equals(o.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            errors.Add($"'{name}' 이름의 작업이 이미 있습니다.");

        string? source = Normalize(job.SourcePath, "대상 폴더", errors);
        string? output = Normalize(job.OutputPath, "출력 폴더", errors);

        if (source is null || output is null)
            return errors;

        Collect(errors, () => LogArchiver.ValidatePaths(job.SourcePath, job.OutputPath));
        Collect(errors, () => SourceRootGuard.EnsureSafe(job.SourcePath));

        foreach (JobConfig other in others)
        {
            string? otherSource = NormalizeQuietly(other.SourcePath);
            string? otherOutput = NormalizeQuietly(other.OutputPath);

            if (otherSource is not null && (IsInside(source, otherSource) || IsInside(otherSource, source)))
                errors.Add($"대상 폴더가 '{other.Name}' 작업의 대상 폴더와 겹칩니다. 같은 파일을 두 작업이 함께 처리하게 됩니다.");

            if (otherOutput is not null && string.Equals(output, otherOutput, StringComparison.OrdinalIgnoreCase))
                errors.Add($"출력 폴더가 '{other.Name}' 작업과 같습니다. 작업마다 다른 출력 폴더를 지정하세요.");

            if (otherSource is not null && IsInside(output, otherSource))
                errors.Add($"출력 폴더가 '{other.Name}' 작업의 대상 폴더 안에 있습니다. 이 작업의 압축 파일을 그 작업이 로그로 알고 처리하게 됩니다.");

            if (otherOutput is not null && IsInside(otherOutput, source))
                errors.Add($"'{other.Name}' 작업의 출력 폴더가 이 작업의 대상 폴더 안에 있습니다. 그 작업의 압축 파일을 이 작업이 로그로 알고 처리하게 됩니다.");
        }

        return errors.Distinct().ToList();
    }

    /// <summary>path가 container와 같거나 그 안에 있는지. 둘 다 끝에 구분자가 붙은 형태여야 한다.</summary>
    private static bool IsInside(string path, string container)
        => path.StartsWith(container, StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string path, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            errors.Add($"{label}를 지정하세요.");
            return null;
        }

        string? normalized = NormalizeQuietly(path);
        if (normalized is null)
            errors.Add($"{label} 경로 형식이 올바르지 않습니다.");

        return normalized;
    }

    private static string? NormalizeQuietly(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim())) + Path.DirectorySeparatorChar;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static void Collect(List<string> errors, Action check)
    {
        try
        {
            check();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            errors.Add(ex.Message);
        }
    }
}
