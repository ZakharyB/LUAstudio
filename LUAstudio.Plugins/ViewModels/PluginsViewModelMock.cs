using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace LUAstudio.Plugins.ViewModels
{
    public class PluginsViewModelMock : INotifyPropertyChanged
    {
        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set { _selectedTabIndex = value; OnPropertyChanged(); }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        private bool _isDevMode;
        public bool IsDevMode
        {
            get => _isDevMode;
            set { _isDevMode = value; OnPropertyChanged(); }
        }

        public ObservableCollection<PluginItem> InstalledPlugins { get; } = new();
        private PluginItem? _selectedInstalledPlugin;
        public PluginItem? SelectedInstalledPlugin
        {
            get => _selectedInstalledPlugin;
            set { _selectedInstalledPlugin = value; OnPropertyChanged(); }
        }

        public ObservableCollection<PluginItem> MarketplacePlugins { get; } = new();
        private PluginItem? _selectedMarketplacePlugin;
        public PluginItem? SelectedMarketplacePlugin
        {
            get => _selectedMarketplacePlugin;
            set { _selectedMarketplacePlugin = value; OnPropertyChanged(); }
        }

        private string _selectedLanguage = "All";
        public string SelectedLanguage { get => _selectedLanguage; set { _selectedLanguage = value; OnPropertyChanged(); } }

        private string _selectedCategory = "All";
        public string SelectedCategory { get => _selectedCategory; set { _selectedCategory = value; OnPropertyChanged(); } }

        private string _selectedSort = "Trending";
        public string SelectedSort { get => _selectedSort; set { _selectedSort = value; OnPropertyChanged(); } }

        private bool _showFavoritesOnly;
        public bool ShowFavoritesOnly { get => _showFavoritesOnly; set { _showFavoritesOnly = value; OnPropertyChanged(); } }

        public ObservableCollection<PluginUpdateInfo> AvailableUpdates { get; } = new();
        private PluginUpdateInfo? _selectedUpdate;
        public PluginUpdateInfo? SelectedUpdate
        {
            get => _selectedUpdate;
            set { _selectedUpdate = value; OnPropertyChanged(); }
        }

        public bool GlobalEnabled { get; set; } = true;
        public bool AutoUpdateAll { get; set; }
        public string UpdateInterval { get; set; } = "Weekly";
        public bool WarnFileAccess { get; set; } = true;
        public bool BlockNetwork { get; set; }
        public bool AllowHooks { get; set; } = true;
        public bool AutoInstallDeps { get; set; } = true;
        public bool ShowConflicts { get; set; } = true;
        public bool DebugLogging { get; set; }
        public bool ShowInternalApis { get; set; }

        public PluginsViewModelMock()
        {
            InstalledPlugins.Add(new PluginItem
            {
                Name = "LuaLinter",
                Author = "StudioTeam",
                Version = "1.2.0",
                IsEnabled = true,
                HealthColor = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)), 
                Icon = null, 
                FullDescription = "Real‑time Lua linting with customisable rules.",
                Permissions = { "File system access", "IDE API" },
                DependencyTree = { new DepNode("LuaParser", new[] { new DepNode("CoreUtils") }) },
                Changelog = { "v1.2.0 – added auto‑fix", "v1.1.0 – performance improvements" },
                Compatibility = "2024.2+"
            });
            InstalledPlugins.Add(new PluginItem
            {
                Name = "DarkMatter Theme",
                Author = "Community",
                Version = "3.0.1",
                IsEnabled = true,
                HealthColor = new SolidColorBrush(Color.FromRgb(0xE5, 0xA8, 0x4B)), 
                FullDescription = "A high‑contrast dark theme with customisable accents.",
                Permissions = { "IDE API" },
            });
            InstalledPlugins.Add(new PluginItem
            {
                Name = "GitBlame",
                Author = "Zachary",
                Version = "0.9.4",
                IsEnabled = false,
                HealthColor = new SolidColorBrush(Color.FromRgb(0xE0, 0x6C, 0x75)),
                FullDescription = "Show Git blame info inline. Requires Git >=2.40.",
                Permissions = { "Process execution" },
                Compatibility = "2024.1+"
            });

            MarketplacePlugins.Add(new PluginItem
            {
                Name = "AI Assistant",
                Author = "LuaStudio Labs",
                ShortDescription = "Local LLM integration for code generation.",
                Rating = 4.8,
                Downloads = 12300,
                Icon = null,
                FullDescription = "Runs a small language model locally to help you write and refactor Lua scripts.",
                Permissions = { "Network (local only)", "File system (sandboxed)" }
            });
            MarketplacePlugins.Add(new PluginItem
            {
                Name = "Debugger Plus",
                Author = "DebugTools",
                ShortDescription = "Visual step‑through debugger for Lua 5.1‑5.4.",
                Rating = 4.6,
                Downloads = 8700,
                FullDescription = "Breakpoints, watches, and an interactive REPL while debugging."
            });
            MarketplacePlugins.Add(new PluginItem
            {
                Name = "Snippets Hub",
                Author = "Community",
                ShortDescription = "Browse and insert community Lua snippets.",
                Rating = 4.2,
                Downloads = 5300,
                FullDescription = "Access hundreds of curated code snippets directly inside the editor."
            });

            AvailableUpdates.Add(new PluginUpdateInfo
            {
                PluginName = "LuaLinter",
                CurrentVersion = "1.2.0",
                NewVersion = "1.3.0",
                ChangelogDiff = "- Auto‑fix now supports 12 new rules.\n- Better performance on large files."
            });
            AvailableUpdates.Add(new PluginUpdateInfo
            {
                PluginName = "DarkMatter Theme",
                CurrentVersion = "3.0.1",
                NewVersion = "3.1.0",
                ChangelogDiff = "- Added 2 new colour presets.\n- Fixed contrast in diff view."
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PluginItem
    {
        public string Name { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string? ShortDescription { get; set; }
        public string FullDescription { get; set; } = string.Empty;
        public double Rating { get; set; }
        public int Downloads { get; set; }
        public bool IsEnabled { get; set; }
        public Brush? HealthColor { get; set; }
        public object? Icon { get; set; }       
        public List<string> Permissions { get; set; } = new();
        public List<DepNode> DependencyTree { get; set; } = new();   
        public List<string> Changelog { get; set; } = new();
        public string Compatibility { get; set; } = "2024.2+";
    }

    public class DepNode
    {
        public string Name { get; set; }
        public List<DepNode> Dependencies { get; set; } = new();
        public DepNode(string name, IEnumerable<DepNode>? deps = null)
        {
            Name = name;
            if (deps != null) Dependencies.AddRange(deps);
        }
    }

    public class PluginUpdateInfo
    {
        public string PluginName { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string NewVersion { get; set; } = string.Empty;
        public string ChangelogDiff { get; set; } = string.Empty;
    }
}