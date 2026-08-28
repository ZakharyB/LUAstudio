using System.Diagnostics;
using System.Text;
using LUAstudio.Core.Events;
using LUAstudio.Workspace;
using LUAstudio.Workspace.Events;

namespace LUAstudio.Git;

public sealed class GitStatusService : IGitStatusService
{
    private readonly IWorkspaceService _workspace;
    private readonly IEventBus _events;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly Action<WorkspaceRootsChangedEvent> _rootsChanged;
    private readonly Action<WorkspaceFileSystemChangedEvent> _filesChanged;
    private IReadOnlyList<GitFileStatus> _files = Array.Empty<GitFileStatus>();
    private IReadOnlyList<GitCommit> _commits = Array.Empty<GitCommit>();

    public GitStatusService(IWorkspaceService workspace, IEventBus events)
    {
        _workspace = workspace;
        _events = events;
        _rootsChanged = _ => QueueRefresh();
        _filesChanged = _ => QueueRefresh();
        events.Subscribe(_rootsChanged);
        events.Subscribe(_filesChanged);
    }

    public event EventHandler? StatusChanged;

    public IReadOnlyList<GitFileStatus> Files => _files;

    public IReadOnlyList<GitCommit> Commits => _commits;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var files = new List<GitFileStatus>();
            var commits = new List<GitCommit>();
            var repositories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in _workspace.Roots)
            {
                var repository = (await RunAsync(root.Path, "rev-parse --show-toplevel", cancellationToken).ConfigureAwait(false)).Trim();
                if (string.IsNullOrWhiteSpace(repository) || !Directory.Exists(repository) || !repositories.Add(repository))
                {
                    continue;
                }

                ParseStatus(repository, await RunAsync(repository, "status --porcelain=v1 -z --untracked-files=all", cancellationToken).ConfigureAwait(false), files);
                ParseCommits(await RunAsync(repository, "log -20 --date=iso-strict --pretty=format:%H%x1f%an%x1f%aI%x1f%s%x1e", cancellationToken).ConfigureAwait(false), commits);
            }

            _files = files.OrderBy(f => f.RepositoryRoot).ThenBy(f => f.RelativePath).ToArray();
            _commits = commits;
        }
        finally
        {
            _refreshLock.Release();
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task StageAsync(GitFileStatus file, CancellationToken cancellationToken = default)
    {
        await RunCheckedAsync(file.RepositoryRoot, $"add -- {Quote(file.RelativePath)}", cancellationToken).ConfigureAwait(false);
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UnstageAsync(GitFileStatus file, CancellationToken cancellationToken = default)
    {
        await RunCheckedAsync(file.RepositoryRoot, $"reset -q HEAD -- {Quote(file.RelativePath)}", cancellationToken).ConfigureAwait(false);
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitAsync(string message, CancellationToken cancellationToken = default)
    {
        var repository = _files.FirstOrDefault(f => f.IsStaged)?.RepositoryRoot
            ?? throw new InvalidOperationException("There are no staged changes to commit.");
        await RunCheckedAsync(repository, $"commit -m {Quote(message)}", cancellationToken).ConfigureAwait(false);
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _events.Unsubscribe(_rootsChanged);
        _events.Unsubscribe(_filesChanged);
        _refreshLock.Dispose();
    }

    private void QueueRefresh() => _ = RefreshAsync();

    private static void ParseStatus(string root, string output, ICollection<GitFileStatus> target)
    {
        var entries = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Length < 4) continue;
            var x = entry[0];
            var y = entry[1];
            var path = entry[3..];
            if ((x is 'R' or 'C') && i + 1 < entries.Length) i++; // Skip the original path in -z rename records.
            var state = ResolveState(x, y);
            target.Add(new GitFileStatus(root, path, Path.GetFullPath(path, root), state, x != ' ' && x != '?'));
        }
    }

    private static GitFileState ResolveState(char x, char y)
    {
        if (x == '?' && y == '?') return GitFileState.Untracked;
        if (x == 'U' || y == 'U' || (x == 'A' && y == 'A') || (x == 'D' && y == 'D')) return GitFileState.Conflicted;
        var value = x == ' ' ? y : x;
        return value switch
        {
            'A' => GitFileState.Added,
            'D' => GitFileState.Deleted,
            'R' or 'C' => GitFileState.Renamed,
            _ => GitFileState.Modified,
        };
    }

    private static void ParseCommits(string output, ICollection<GitCommit> target)
    {
        foreach (var record in output.Split('\x1e', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = record.Trim().Split('\x1f');
            if (fields.Length == 4 && DateTimeOffset.TryParse(fields[2], out var authoredAt))
                target.Add(new GitCommit(fields[0], fields[3], fields[1], authoredAt));
        }
    }

    private static async Task<string> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        try
        {
            using var process = Process.Start(startInfo);
            if (process is null) return string.Empty;
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await errorTask.ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            return process.ExitCode == 0 ? output : string.Empty;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }

    private static async Task RunCheckedAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git", arguments) { WorkingDirectory = workingDirectory, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Git could not be started.");
        var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0) throw new InvalidOperationException(error.Trim());
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}
