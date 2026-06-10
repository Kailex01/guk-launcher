using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using GukLauncher.Models;
using GukLauncher.Services;

namespace GukLauncher.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly HttpClient _http;
    private readonly string _installDir;

    // ── Bound properties ───────────────────────────────────────────────────────

    private string _statusMessage = "Initializing...";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    private string _progressDetail = "";
    public string ProgressDetail
    {
        get => _progressDetail;
        set { _progressDetail = value; OnPropertyChanged(); }
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    private bool _canPlay;
    public bool CanPlay
    {
        get => _canPlay;
        set
        {
            _canPlay = value;
            OnPropertyChanged();
            Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        }
    }

    public ObservableCollection<PatchNote> PatchNotes { get; } = new();
    public ICommand PlayCommand { get; }

    // ── Constructor ────────────────────────────────────────────────────────────

    public MainViewModel()
    {
        _installDir = AppDomain.CurrentDomain.BaseDirectory;

        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("GukLauncher/1.0");

        PlayCommand = new RelayCommand(_ => LaunchGame(), _ => CanPlay);
    }

    // ── Startup ────────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        await Task.WhenAll(LoadPatchNotesAsync(), RunPatcherAsync());
    }

    private async Task LoadPatchNotesAsync()
    {
        try
        {
            var notes = await new PatchNotesService(_http).FetchAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                PatchNotes.Clear();
                foreach (var n in notes)
                    PatchNotes.Add(n);
            });
        }
        catch { /* cosmetic — silently skip if GitHub is unreachable */ }
    }

    private async Task RunPatcherAsync()
    {
        // These Progress<T> objects are created on the UI thread (Loaded event),
        // so their callbacks are automatically marshalled back to the UI thread.
        var statusProgress = new Progress<string>(msg => StatusMessage = msg);
        var dlProgress = new Progress<DownloadProgress>(p =>
        {
            Progress       = p.Percentage;
            ProgressDetail = $"{p.BytesDownloadedMb} / {p.BytesTotalMb}  ({p.FilesCompleted}/{p.FilesTotal} files)";
            if (!string.IsNullOrEmpty(p.CurrentFile))
                StatusMessage = p.CurrentFile;
        });

        try
        {
            var patcherSvc = new PatcherService(new ManifestService(_http), _installDir);
            var queue      = await patcherSvc.BuildQueueAsync(statusProgress);

            if (queue.Count == 0)
            {
                SetUI(() => { StatusMessage = "Up to date"; Progress = 100; ProgressDetail = ""; CanPlay = true; });
                return;
            }

            long totalBytes = patcherSvc.GetTotalDownloadSize(queue);
            SetUI(() => StatusMessage = $"Downloading {queue.Count:N0} file(s)  ({totalBytes / 1024.0 / 1024.0:F0} MB)...");

            await new DownloadService(_http).DownloadAllAsync(queue, _installDir, dlProgress);

            SetUI(() => { StatusMessage = "Up to date"; Progress = 100; ProgressDetail = ""; CanPlay = true; });
        }
        catch (Exception ex)
        {
            SetUI(() => { StatusMessage = $"Patcher error: {ex.Message}"; CanPlay = true; });
        }
    }

    // ── Game launch ────────────────────────────────────────────────────────────

    private void LaunchGame()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName         = Path.Combine(_installDir, "eqgame.exe"),
            Arguments        = "patchme",
            WorkingDirectory = _installDir,
            UseShellExecute  = true,
        });
        Application.Current.MainWindow.WindowState = WindowState.Minimized;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void SetUI(Action action) =>
        Application.Current.Dispatcher.Invoke(action);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
