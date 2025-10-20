using System;
using Microsoft.Win32;
using RandomGamePicker.Models;
using RandomGamePicker.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RandomGamePicker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<GameEntry> _games = new();
        private readonly CollectionViewSource _viewSource = new();
        private readonly Random _rng = Random.Shared;
        private GameEntry? _lastRolled;

        // Keyboard command for Ctrl+R
        private readonly RoutedUICommand _rollAndRunCommand =
            new("Roll & Run", "RollAndRun", typeof(MainWindow));

        public MainWindow()
        {
            InitializeComponent();

            // Load saved list (if any), otherwise auto-scan desktop shortcuts
            var loaded = GameStore.Load();
            if (loaded.Count == 0)
            {
                var scanned = ScanDesktopShortcuts();
                foreach (var g in scanned) _games.Add(g);
                GameStore.Save(_games);
            }
            else
            {
                foreach (var g in loaded) _games.Add(g);
            }

            _viewSource.Source = _games;
            _viewSource.Filter += ApplyFilter;
            GamesGrid.ItemsSource = _viewSource.View;

            CommandBindings.Add(new CommandBinding(
                SystemCommands.CloseWindowCommand,
                (s, e) => SystemCommands.CloseWindow(this)));

            CommandBindings.Add(new CommandBinding(
                SystemCommands.MinimizeWindowCommand,
                (s, e) => SystemCommands.MinimizeWindow(this)));

            CommandBindings.Add(new CommandBinding(
                SystemCommands.MaximizeWindowCommand,
                (s, e) => SystemCommands.MaximizeWindow(this)));

            CommandBindings.Add(new CommandBinding(
                SystemCommands.RestoreWindowCommand,
                (s, e) => SystemCommands.RestoreWindow(this)));

            // Ctrl+R = Roll & Run
            InputBindings.Add(new KeyBinding(
                _rollAndRunCommand,
                new KeyGesture(Key.R, ModifierKeys.Control)));
            CommandBindings.Add(new CommandBinding(
                _rollAndRunCommand, (s, e) => RollAndRun()));
        }

        // --- UI Actions ---
        private void RescanDesktop_Click(object sender, RoutedEventArgs e)
        {
            var existingPaths = new HashSet<string>(_games.Select(g => g.Path), StringComparer.OrdinalIgnoreCase);
            int added = 0;
            foreach (var g in ScanDesktopShortcuts())
            {
                if (existingPaths.Add(g.Path)) { _games.Add(g); added++; }
            }
            GameStore.Save(_games);
            StatusText.Text = added > 0 ? $"Added {added} new item(s)." : "No new shortcuts found on Desktop.";
        }

        private void AddShortcuts_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Add game shortcuts or executables",
                Filter = "Shortcuts / Executables / Steam URLs|*.lnk;*.exe;*.url|All files|*.*",
                Multiselect = true
            };
            if (ofd.ShowDialog(this) == true)
            {
                int added = 0;
                var existing = new HashSet<string>(_games.Select(g => g.Path), StringComparer.OrdinalIgnoreCase);
                foreach (var path in ofd.FileNames)
                {
                    if (existing.Contains(path)) continue;
                    _games.Add(new GameEntry { Name = System.IO.Path.GetFileNameWithoutExtension(path), Path = path, Included = true });
                    added++;
                }
                if (added > 0) GameStore.Save(_games);
                StatusText.Text = added > 0 ? $"Added {added} item(s)." : "Nothing added.";
            }
        }

        private void RemoveMissing_Click(object sender, RoutedEventArgs e)
        {
            var missing = _games.Where(g => !File.Exists(g.Path)).ToList();
            foreach (var m in missing) _games.Remove(m);
            if (missing.Count > 0) GameStore.Save(_games);
            StatusText.Text = missing.Count > 0 ? $"Removed {missing.Count} missing item(s)." : "No missing items.";
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var g in _games) g.Included = true;
            GamesGrid.Items.Refresh();
            GameStore.Save(_games);
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var g in _games) g.Included = false;
            GamesGrid.Items.Refresh();
            GameStore.Save(_games);
        }

        private void Roll_Click(object sender, RoutedEventArgs e)
        {
            var pool = _games.Where(g => g.Included).ToList();
            if (pool.Count == 0)
            {
                StatusText.Text = "No games are included. Check some boxes first.";
                _lastRolled = null;
                return;
            }
            _lastRolled = pool[_rng.Next(pool.Count)];
            StatusText.Text = $"Rolled: {_lastRolled.Name}";
        }

        private void RollAndRun()
        {
            var pool = _games.Where(g => g.Included).ToList();
            if (pool.Count == 0)
            {
                StatusText.Text = "No games are included. Check some boxes first.";
                _lastRolled = null;
                return;
            }

            var pick = pool[_rng.Next(pool.Count)];
            _lastRolled = pick;
            StatusText.Text = $"Rolled: {pick.Name}";

            Launch(pick);
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            if (_lastRolled == null)
            {
                // If not rolled yet, roll now
                Roll_Click(sender, e);
                if (_lastRolled == null) return;
            }

            Launch(_lastRolled);
        }

        private void Launch(GameEntry g)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = g.Path,
                    UseShellExecute = true // allows launching .lnk and .url (shell resolves)
                };
                Process.Start(psi);
                StatusText.Text = $"Launching: {g.Name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to launch '{g.Name}'.\n{ex.Message}",
                    "Launch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnRollRun_Click(object sender, RoutedEventArgs e) => RollAndRun();

        // --- Drag & Drop support ---
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            int added = 0;
            var existing = new HashSet<string>(_games.Select(g => g.Path), StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
            {
                if (!File.Exists(f)) continue;
                if (ShouldIncludePath(f)) // accepts .lnk, .exe, and Steam .url
                {
                    if (existing.Add(f))
                    {
                        _games.Add(new GameEntry { Name = System.IO.Path.GetFileNameWithoutExtension(f), Path = f, Included = true });
                        added++;
                    }
                }
            }
            if (added > 0) GameStore.Save(_games);
            StatusText.Text = added > 0 ? $"Added {added} item(s) via drag & drop." : "No new items from drop.";
        }

        // --- Filtering ---
        private void ApplyFilter(object? sender, FilterEventArgs e)
        {
            if (e.Item is not GameEntry g) { e.Accepted = false; return; }

            if (OnlyShowIncluded.IsChecked == true && !g.Included) { e.Accepted = false; return; }

            var q = SearchBox.Text?.Trim();
            if (!string.IsNullOrEmpty(q))
            {
                e.Accepted = g.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || g.Path.Contains(q, StringComparison.OrdinalIgnoreCase);
                return;
            }
            e.Accepted = true;
        }

        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            _viewSource.View.Refresh();
        }

        // --- Helpers ---
        private static IEnumerable<GameEntry> ScanDesktopShortcuts()
        {
            string[] desktops =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };

            var results = new List<GameEntry>();
            foreach (var desk in desktops)
            {
                if (Directory.Exists(desk))
                {
                    foreach (var file in Directory.EnumerateFiles(desk, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        if (!ShouldIncludePath(file)) continue;
                        results.Add(new GameEntry
                        {
                            Name = System.IO.Path.GetFileNameWithoutExtension(file),
                            Path = file,
                            Included = true
                        });
                    }
                }
            }
            return results;
        }

        private static bool ShouldIncludePath(string path)
        {
            var ext = System.IO.Path.GetExtension(path);
            if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)) return true;
            if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)) return true;
            if (ext.Equals(".url", StringComparison.OrdinalIgnoreCase)) return IsSteamUrlFile(path);
            return false;
        }

        private static bool IsSteamUrlFile(string path)
        {
            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    // .url is INI-like, with a line: URL=steam://rungameid/xxxx
                    if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    {
                        var url = line[4..].Trim();
                        return url.StartsWith("steam://", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch { /* ignore unreadable .url */ }
            return false;
        }

        protected override void OnClosed(EventArgs e)
        {
            GameStore.Save(_games);
            base.OnClosed(e);
        }
    }
}
