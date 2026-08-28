using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LUAstudio.Git;

public sealed partial class SourceControlViewModel : ObservableObject
{
    private readonly IGitStatusService _git;
    private readonly SynchronizationContext? _context;

    public SourceControlViewModel(IGitStatusService git)
    {
        _git = git;
        _context = SynchronizationContext.Current;
        _git.StatusChanged += (_, _) => RunOnContext(UpdateCollections);
    }

    public ObservableCollection<GitFileStatus> Changes { get; } = new();
    public ObservableCollection<GitCommit> Commits { get; } = new();

    [ObservableProperty] private string _commitMessage = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public Task InitializeAsync() => ExecuteAsync(ct => _git.RefreshAsync(ct));

    [RelayCommand]
    private Task RefreshAsync() => ExecuteAsync(ct => _git.RefreshAsync(ct));

    [RelayCommand]
    private Task ToggleStageAsync(GitFileStatus? file) => file is null
        ? Task.CompletedTask
        : ExecuteAsync(ct => file.IsStaged ? _git.UnstageAsync(file, ct) : _git.StageAsync(file, ct));

    [RelayCommand(CanExecute = nameof(CanCommit))]
    private async Task CommitAsync()
    {
        var message = CommitMessage.Trim();
        await ExecuteAsync(ct => _git.CommitAsync(message, ct)).ConfigureAwait(true);
        if (ErrorMessage is null) CommitMessage = string.Empty;
    }

    private bool CanCommit() => !IsBusy && !string.IsNullOrWhiteSpace(CommitMessage) && Changes.Any(c => c.IsStaged);

    partial void OnCommitMessageChanged(string value) => CommitCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => CommitCommand.NotifyCanExecuteChanged();

    private async Task ExecuteAsync(Func<CancellationToken, Task> action)
    {
        IsBusy = true;
        ErrorMessage = null;
        try { await action(CancellationToken.None).ConfigureAwait(true); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    private void UpdateCollections()
    {
        Changes.Clear();
        foreach (var file in _git.Files) Changes.Add(file);
        Commits.Clear();
        foreach (var commit in _git.Commits) Commits.Add(commit);
        CommitCommand.NotifyCanExecuteChanged();
    }

    private void RunOnContext(Action action)
    {
        if (_context is null) action();
        else _context.Post(_ => action(), null);
    }
}
