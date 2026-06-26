using LUAstudio.Abstractions;
using LUAstudio.Core;
using LUAstudio.IDE.ViewModels;
using LUAstudio.Settings.Views;
using LUAstudio.Execution;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Themes;

namespace LUAstudio;

public partial class MainWindow
{
    private const int WmGetMinMaxInfoMessage = 0x0024;
    private const uint MonitorDefaultToNearest = 2;

    public DebugPanelViewModel DebugPanelViewModel { get; }

    public MainWindow(MainViewModel viewModel, DiagnosticsPanelViewModel diagnosticsPanel)
    {
        InitializeComponent();
        DataContext = viewModel;
        DiagnosticsPanel.DataContext = diagnosticsPanel;
        DebugPanelViewModel = new DebugPanelViewModel();
        DebugPanel.DataContext = DebugPanelViewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
        SourceInitialized += OnSourceInitialized;
        UpdateMaximizeButton();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowProc);
        }
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfoMessage)
        {
            ApplyMaximizedWorkArea(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void ApplyMaximizedWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var work = monitorInfo.rcWork;
        var monitorRect = monitorInfo.rcMonitor;
        mmi.ptMaxPosition.X = Math.Abs(work.Left - monitorRect.Left);
        mmi.ptMaxPosition.Y = Math.Abs(work.Top - monitorRect.Top);
        mmi.ptMaxSize.X = Math.Abs(work.Right - work.Left);
        mmi.ptMaxSize.Y = Math.Abs(work.Bottom - work.Top);
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    private void TitleBar_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsTitleBarInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            e.Handled = true;
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
            e.Handled = true;
        }
    }

    private static bool IsTitleBarInteractiveElement(DependencyObject? source)
    {
        for (var current = source; current is not null; current = current.GetParentObject())
        {
            if (current is Button or MenuItem or Menu or System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }
        }

        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point ptReserved;
        public Point ptMaxSize;
        public Point ptMaxPosition;
        public Point ptMinTrackSize;
        public Point ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_OnClick(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Window_StateChanged(object? sender, EventArgs e) => UpdateMaximizeButton();

    private void UpdateMaximizeButton()
    {
        if (MaximizeButton is null)
        {
            return;
        }

        var maximized = WindowState == WindowState.Maximized;
        MaximizeButton.Content = maximized ? "\uE923" : "\uE922";
        MaximizeButton.ToolTip = maximized ? "Restore" : "Maximize";
    }

    private void OpenSettings_OnClick(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow { Owner = this };
        window.ShowDialog();
    }

    private async void RunActiveDocument_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.ActiveDocument is null)
        {
            return;
        }

        await DebugPanelViewModel.RunDocumentAsync(vm.ActiveDocument.Content ?? string.Empty, vm.ActiveDocument.FilePath);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (DataContext is MainViewModel vm)
        {
            await vm.InitializeAsync().ConfigureAwait(true);
            vm.RestoreWorkspaceOnStartup = Engine.Globals.Get<bool>(SettingKeys.RestoreWorkspaceRoots)?.Value ?? vm.RestoreWorkspaceOnStartup;
        }

        DockLayoutStore.DeleteLegacyLayoutFile();
        DockManager.Theme = new Vs2013DarkTheme();
        EnsureDockDefaults();

        try
        {
            var client = new ExecutionHostClient();
            await DebugPanelViewModel.InitializeAsync(client);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize debug panel: {ex.Message}");
        }
    }

    private void EnsureDockDefaults()
    {
        var explorer = FindAnchorable("Explorer");
        if (explorer is not null)
        {
            explorer.IsSelected = true;
            explorer.Show();
        }

        var problems = FindAnchorable("Problems");
        problems?.Show();

        var output = FindAnchorable("Output");
        output?.Show();

        var debug = FindAnchorable("Debug");
        debug?.Show();
    }

    private LayoutAnchorable? FindAnchorable(string contentId)
    {
        if (DockManager.Layout is null)
        {
            return null;
        }

        foreach (var anchorable in DockManager.Layout.Descendents().OfType<LayoutAnchorable>())
        {
            if (string.Equals(anchorable.ContentId, contentId, StringComparison.Ordinal))
            {
                return anchorable;
            }
        }

        return null;
    }

    private void ResetDockLayout_OnClick(object sender, RoutedEventArgs e)
    {
        if (DockManager.Layout is null)
        {
            return;
        }

        foreach (var anchorable in DockManager.Layout.Descendents().OfType<LayoutAnchorable>())
        {
            anchorable.Show();
            if (string.Equals(anchorable.ContentId, "Explorer", StringComparison.Ordinal))
            {
                anchorable.IsSelected = true;
            }
        }

        EnsureDockDefaults();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Intentionally do not persist AvalonDock XML — it previously broke Explorer bindings.
    }
}
