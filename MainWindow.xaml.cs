using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ClearHistory;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<CategoryRow> _categories = new();
    private List<HistoryEntry> _all = new();
    private readonly string _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearHistory", "operations.log");
    private static readonly string[] Categories = ["Documents", "Images", "Videos", "Audio", "Archives", "Other files"];
    public MainWindow()
    {
        InitializeComponent();
        ApplyRecentFolderIcon();
        foreach (var c in Categories) _categories.Add(new CategoryRow(c));
        CategoryList.ItemsSource = _categories;
        RefreshData();
    }
    private void ApplyRecentFolderIcon()
    {
        var iconInfo = new ShellIconInfo();
        var result = SHGetFileInfo(RecentDiscovery.RecentPath, 0, ref iconInfo, (uint)Marshal.SizeOf<ShellIconInfo>(), ShgfiIcon | ShgfiSmallIcon);
        if (result == IntPtr.Zero || iconInfo.IconHandle == IntPtr.Zero) return;
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(iconInfo.IconHandle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            Icon = source;
        }
        finally { DestroyIcon(iconInfo.IconHandle); }
    }
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallIcon = 0x000000001;
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellIconInfo
    {
        public IntPtr IconHandle;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint fileAttributes, ref ShellIconInfo fileInfo, uint fileInfoSize, uint flags);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
    private void RangeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FromDate == null) return;
        var custom = RangeBox.SelectedIndex == 5;
        FromDate.Visibility = ToDate.Visibility = DateDash.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        UpdateView();
    }
    private void RefreshClick(object sender, RoutedEventArgs e) => RefreshData();
    private void CategoryChanged(object sender, RoutedEventArgs e) => UpdateView();
    private void RefreshData()
    {
        _all = RecentDiscovery.Scan();
        UpdateView();
    }
    private void UpdateView()
    {
        if (CategoryList == null) return;
        var cutoff = GetCutoff();
        var end = RangeBox.SelectedIndex == 5 && ToDate.SelectedDate.HasValue ? ToDate.SelectedDate.Value.Date.AddDays(1) : DateTime.Now;
        var filtered = _all.Where(x => x.Timestamp >= cutoff && x.Timestamp < end).ToList();
        foreach (var cat in _categories)
        {
            cat.Count = filtered.Count(x => x.Category == cat.Name);
        }
        var selected = _categories.Where(c => c.IsSelected).Select(c => c.Name).ToHashSet();
        var items = filtered.Where(x => selected.Contains(x.Category)).OrderByDescending(x => x.Timestamp).ToList();
        PreviewGrid.ItemsSource = items;
        PreviewSummary.Text = $"{items.Count} matching shortcut{(items.Count == 1 ? "" : "s")} · {RecentDiscovery.RecentPath}";
        StatusText.Text = $"{_all.Count} shortcuts discovered · {items.Count} in preview";
    }
    private DateTime GetCutoff() => RangeBox.SelectedIndex switch
    {
        0 => DateTime.Now.AddHours(-1), 1 => DateTime.Now.AddHours(-24), 2 => DateTime.Now.AddDays(-7), 3 => DateTime.Now.AddDays(-30),
        4 => DateTime.MinValue, 5 when FromDate.SelectedDate.HasValue => FromDate.SelectedDate.Value, _ => DateTime.MinValue
    };
    private void ClearClick(object sender, RoutedEventArgs e)
    {
        var cutoff = GetCutoff();
        var selected = _categories.Where(c => c.IsSelected).Select(c => c.Name).ToHashSet();
        var end = RangeBox.SelectedIndex == 5 && ToDate.SelectedDate.HasValue ? ToDate.SelectedDate.Value.Date.AddDays(1) : DateTime.Now;
        var targets = _all.Where(x => selected.Contains(x.Category) && x.Timestamp >= cutoff && x.Timestamp < end).ToList();
        if (targets.Count == 0) { MessageBox.Show("There are no matching entries to clear.", "Clear Recent Activity", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var rangeName = (RangeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Custom range";
        var message = $"Remove {targets.Count} recent shortcut{(targets.Count == 1 ? "" : "s")}?\n\nCategories: {string.Join(", ", selected)}\nTime range: {rangeName}\n\nOriginal files will not be touched.";
        if (MessageBox.Show(message, DryRun.IsChecked == true ? "Dry run confirmation" : "Confirm clearing history", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (DryRun.IsChecked == true) { Log($"DRY RUN: {targets.Count} entries matched; no changes made."); MessageBox.Show($"Dry run complete. {targets.Count} shortcut(s) would be removed. No changes were made.", "Dry run", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var success = 0; var failures = new List<string>();
        foreach (var entry in targets)
        {
            try { File.Delete(entry.ShortcutPath); success++; Log($"Removed shortcut: {entry.ShortcutPath}"); }
            catch (Exception ex) { failures.Add($"{entry.Name}: {ex.Message}"); Log($"FAILED {entry.ShortcutPath}: {ex}"); }
        }
        RefreshData();
        var report = $"Cleared {success} of {targets.Count} recent shortcut(s).\n\nExplorer may cache recent-item views; refresh Explorer or sign out if a removed item still appears. Jump Lists and Start recommendations are separate stores and were not changed." + (failures.Count > 0 ? $"\n\nCould not clear {failures.Count}:\n" + string.Join("\n", failures.Take(8)) : "");
        MessageBox.Show(report, "Clear report", MessageBoxButton.OK, failures.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
    private void Log(string message)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!); File.AppendAllText(_logPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"); } catch { }
    }
}

public sealed class CategoryRow : System.ComponentModel.INotifyPropertyChanged
{
    public string Name { get; }
    private int _count;
    public int Count { get => _count; set { _count = value; PropertyChanged?.Invoke(this, new(nameof(Count))); } }
    public bool IsSelected { get; set; } = true;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public CategoryRow(string name) => Name = name;
}

public sealed class HistoryEntry
{
    public required string ShortcutPath { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Category { get; init; }
    public required DateTime Timestamp { get; init; }
    public string TimestampText => Timestamp.ToString("g");
    public string Source => "Explorer Recent";
    public string Location { get; init; } = "Unknown";
}

public static class RecentDiscovery
{
    public static string RecentPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Recent");
    private static readonly HashSet<string> Images = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".heic", ".svg" };
    private static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".webm", ".m4v" };
    private static readonly HashSet<string> Audio = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".wma" };
    private static readonly HashSet<string> Archives = new(StringComparer.OrdinalIgnoreCase) { ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz" };
    public static List<HistoryEntry> Scan()
    {
        var result = new List<HistoryEntry>();
        if (!Directory.Exists(RecentPath)) return result;
        foreach (var file in Directory.EnumerateFiles(RecentPath, "*.lnk", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var target = ResolveTarget(file);
                if (string.IsNullOrWhiteSpace(target)) continue;
                var ext = Path.GetExtension(target);
                var cat = Images.Contains(ext) ? "Images" : Videos.Contains(ext) ? "Videos" : Audio.Contains(ext) ? "Audio" : Archives.Contains(ext) ? "Archives" : IsDocument(ext) ? "Documents" : "Other files";
                var stamp = File.GetLastWriteTime(file);
                result.Add(new HistoryEntry { ShortcutPath = file, Name = Path.GetFileName(target), Type = string.IsNullOrEmpty(ext) ? "File" : ext.TrimStart('.').ToUpperInvariant(), Category = cat, Timestamp = stamp, Location = Path.GetDirectoryName(target) ?? target });
            }
            catch { }
        }
        return result;
    }
    private static string? ResolveTarget(string shortcut)
    {
        object? shell = null; object? link = null;
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) return null;
            shell = Activator.CreateInstance(type);
            link = type.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [shortcut]);
            return link?.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, link, null) as string;
        }
        finally
        {
            if (link != null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
            if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }
    private static bool IsDocument(string ext) => new[] { ".doc", ".docx", ".pdf", ".txt", ".rtf", ".xls", ".xlsx", ".ppt", ".pptx", ".csv", ".odt", ".md" }.Contains(ext, StringComparer.OrdinalIgnoreCase);
}
