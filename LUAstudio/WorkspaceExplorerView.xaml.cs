using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using LUAstudio.IDE.ViewModels;
using LUAstudio.Workspace;

namespace LUAstudio;

public partial class WorkspaceExplorerView : UserControl
{
    private Point _dragStartPoint;
    private FileSystemEntryNode? _dragRootNode;
    private bool _filterExpandPending;

    public WorkspaceExplorerView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += (_, _) => HookFilterExpand();
    }

    private WorkspaceExplorerViewModel? Vm => DataContext as WorkspaceExplorerViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WorkspaceTree.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OnItemExpanded), handledEventsToo: true);
        HookFilterExpand();
        FilterBox.Focusable = true;
    }

    private void HookFilterExpand()
    {
        if (Vm is null)
        {
            return;
        }

        Vm.PropertyChanged -= OnViewModelPropertyChanged;
        Vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceExplorerViewModel.FilterText) or nameof(WorkspaceExplorerViewModel.IsFilterActive))
        {
            ScheduleFilterExpand();
        }
    }

    private void ScheduleFilterExpand()
    {
        if (_filterExpandPending)
        {
            return;
        }

        _filterExpandPending = true;
        Dispatcher.BeginInvoke(() =>
        {
            _filterExpandPending = false;
            ExpandFilterMatches();
        }, DispatcherPriority.Loaded);
    }

    private void ExpandFilterMatches()
    {
        if (Vm is null || !Vm.IsFilterActive)
        {
            return;
        }

        foreach (var node in Vm.GetFilterExpandNodes())
        {
            var container = FindContainer(node);
            if (container is not null)
            {
                container.IsExpanded = true;
            }
        }
    }

    private TreeViewItem? FindContainer(FileSystemEntryNode node)
    {
        foreach (var item in WorkspaceTree.Items)
        {
            if (item is not FileSystemEntryNode root)
            {
                continue;
            }

            var container = WorkspaceTree.ItemContainerGenerator.ContainerFromItem(root) as TreeViewItem;
            var found = FindContainerRecursive(container, node);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static TreeViewItem? FindContainerRecursive(TreeViewItem? parent, FileSystemEntryNode target)
    {
        if (parent is null)
        {
            return null;
        }

        if (ReferenceEquals(parent.DataContext, target))
        {
            return parent;
        }

        parent.UpdateLayout();
        foreach (var childObj in parent.Items)
        {
            if (childObj is not FileSystemEntryNode childNode)
            {
                continue;
            }

            if (parent.ItemContainerGenerator.ContainerFromItem(childNode) is not TreeViewItem childItem)
            {
                continue;
            }

            var found = FindContainerRecursive(childItem, target);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }



    private void WorkspaceTree_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var item = source.FindAncestor<TreeViewItem>();
        if (item is not null)
        {
            item.IsSelected = true;
            item.Focus();
            _dragStartPoint = e.GetPosition(null);
            _dragRootNode = item.DataContext as FileSystemEntryNode;
            if (_dragRootNode is { IsWorkspaceRoot: false })
            {
                _dragRootNode = null;
            }
        }
    }

    private void WorkspaceTree_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var item = source.FindAncestor<TreeViewItem>();
        var node = item?.DataContext as FileSystemEntryNode;
        if (item is not null)
        {
            item.IsSelected = true;
            item.Focus();
        }

        var menu = BuildContextMenu(node);
        menu.PlacementTarget = (UIElement?)item ?? WorkspaceTree;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu BuildContextMenu(FileSystemEntryNode? node)
    {
        var menu = new ContextMenu();   // The ContextMenu style from resources will apply
        // No Resources added – MenuItems use the global implicit style

        if (Vm is null) return menu;

        if (node is null)
        {
            menu.Items.Add(Menu("Open folder…", Vm.AddWorkspaceRootCommand, null));
            return menu;
        }

        var parentForNew = node.IsDirectory && !node.IsTruncationPlaceholder ? node : node;
        menu.Items.Add(Menu("New File", Vm.NewFileCommand, parentForNew));
        menu.Items.Add(Menu("New Folder", Vm.NewFolderCommand, parentForNew));
        menu.Items.Add(new Separator());

        if (!node.IsTruncationPlaceholder)
        {
            menu.Items.Add(Menu("Rename", Vm.RenameEntryCommand, node));
            menu.Items.Add(Menu("Delete", Vm.DeleteEntryCommand, node));
            menu.Items.Add(new Separator());
            menu.Items.Add(Menu("Reveal in Explorer", Vm.RevealInExplorerCommand, node));
            menu.Items.Add(Menu("Copy Path", Vm.CopyPathCommand, node));
            menu.Items.Add(Menu("Open in Terminal", Vm.OpenInTerminalCommand, node));
        }

        if (node.IsWorkspaceRoot)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Menu("Remove Folder from Workspace", Vm.RemoveWorkspaceRootCommand, node));
            menu.Items.Add(Menu("Move Up", Vm.MoveWorkspaceRootUpCommand, node));
            menu.Items.Add(Menu("Move Down", Vm.MoveWorkspaceRootDownCommand, node));
        }

        return menu;
    }

    private static MenuItem Menu(string header, IRelayCommand command, object? parameter)
    {
        return new MenuItem
        {
            Header = header,
            Command = command,
            CommandParameter = parameter,
        };
    }

    private void TreeViewItem_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragRootNode is null || sender is not TreeViewItem item)
        {
            return;
        }

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var index = Vm?.RootNodes.IndexOf(_dragRootNode) ?? -1;
        if (index < 0)
        {
            return;
        }

        DragDrop.DoDragDrop(item, index, DragDropEffects.Move);
        _dragRootNode = null;
    }

    private void TreeViewItem_OnDragOver(object sender, DragEventArgs e)
    {
        if (sender is not TreeViewItem { DataContext: FileSystemEntryNode node } || !node.IsWorkspaceRoot)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = e.Data.GetDataPresent(typeof(int)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void TreeViewItem_OnDrop(object sender, DragEventArgs e)
    {
        if (Vm is null || sender is not TreeViewItem { DataContext: FileSystemEntryNode target } || !target.IsWorkspaceRoot)
        {
            return;
        }

        if (!e.Data.GetDataPresent(typeof(int)))
        {
            return;
        }

        var fromIndex = (int)e.Data.GetData(typeof(int))!;
        var toIndex = Vm.RootNodes.IndexOf(target);
        if (fromIndex >= 0 && toIndex >= 0 && fromIndex != toIndex)
        {
            _ = Vm.ReorderRootsAsync(fromIndex, toIndex);
        }

        e.Handled = true;
    }

    private void WorkspaceTree_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(int)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void WorkspaceTree_OnDrop(object sender, DragEventArgs e)
    {
        if (Vm is null || !e.Data.GetDataPresent(typeof(int)))
        {
            return;
        }

        var fromIndex = (int)e.Data.GetData(typeof(int))!;
        var toIndex = Vm.RootNodes.Count - 1;
        if (fromIndex >= 0 && toIndex >= 0 && fromIndex != toIndex)
        {
            await Vm.ReorderRootsAsync(fromIndex, toIndex);
        }

        e.Handled = true;
    }

    private async void OnItemExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem { DataContext: FileSystemEntryNode node } || Vm is null)
        {
            return;
        }

        await Vm.TreeItemExpandedCommand.ExecuteAsync(node);
        ScheduleFilterExpand();
    }

    private async void WorkspaceTree_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var item = source.FindAncestor<TreeViewItem>();
        if (item?.DataContext is not FileSystemEntryNode node || node.IsDirectory || node.IsTruncationPlaceholder)
        {
            return;
        }

        e.Handled = true;
        if (Vm is not null)
        {
            await Vm.OpenEntryCommand.ExecuteAsync(node);
        }
    }

    private async void WorkspaceTree_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Vm is not null && Vm.IsFilterActive)
        {
            Vm.ClearFilterCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || WorkspaceTree.SelectedItem is not FileSystemEntryNode node || Vm is null)
        {
            return;
        }

        e.Handled = true;
        if (node.IsDirectory)
        {
            await Vm.TreeItemExpandedCommand.ExecuteAsync(node);
        }
        else
        {
            await Vm.OpenEntryCommand.ExecuteAsync(node);
        }
    }

    private void FilterBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Vm is not null)
        {
            Vm.ClearFilterCommand.Execute(null);
            e.Handled = true;
        }
    }

}
