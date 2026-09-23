namespace LogRogue.Core.Diagnostics;

/// <summary>프로그램 자체 동작 기록.</summary>
public interface IAppLog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
}

/// <summary>아무것도 기록하지 않는 구현. 테스트나 로그가 필요 없는 곳에서 쓴다.</summary>
public sealed class NullLog : IAppLog
{
    public static readonly NullLog Instance = new();

    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message, Exception? exception = null) { }
}
