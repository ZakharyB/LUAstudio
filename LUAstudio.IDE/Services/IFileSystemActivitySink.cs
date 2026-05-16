namespace LUAstudio.IDE.Services;

public interface IFileSystemActivitySink
{
    void ReportFileSystemActivity(string? message);
}
