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
    private List<PatchJob>? _pendingQueue;

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

    private bool _patchNeeded;
    public bool PatchNeeded
    {
        get => _patchNeeded;
        set
        {
            _patchNeeded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowPatchButton));
            OnPropertyChanged(nameof(ShowPlayButton));
            Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        }
    }

    private bool _isPatching;
    public bool IsPatching
    {
        get => _isPatching;
        set
        {
            _isPatching = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowPatchButton));
            OnPropertyChanged(nameof(ShowPlayButton));
            Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        }
    }

    private string _patchButtonText = "PATCH";
    public string PatchButtonText
    {
        get => _patchButtonText;
        set { _patchButtonText = value; OnPropertyChanged(); }
    }

    // Computed visibility helpers — PATCH and PLAY share the same slot
    public bool ShowPatchButton => PatchNeeded || IsPatching;
    public bool ShowPlayButton  => !PatchNeeded && !IsPatching;

    public ObservableCollection<PatchNote> PatchNotes { get; } = new();
    public ICommand PlayCommand  { get; }
    public ICommand PatchCommand { get; }

    // ── Constructor ────────────────────────────────────────────────────────────

    public MainViewModel()
    {
        _installDir = AppDomain.CurrentDomain.BaseDirectory;

        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("GukLauncher/1.0");

        PlayCommand  = new RelayCommand(_ => LaunchGame(),            _ => CanPlay);
        PatchCommand = new RelayCommand(async _ => await StartPatchingAsync(), _ => PatchNeeded && !IsPatching);
    }

    // ── Startup ────────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        await Task.WhenAll(LoadPatchNotesAsync(), CheckForUpdatesAsync());
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

    private async Task CheckForUpdatesAsync()
    {
        var statusProgress = new Progress<string>(msg => StatusMessage = msg);
        try
        {
            StatusMessage = "Checking for updates...";
            var patcherSvc = new PatcherService(new ManifestService(_http), _installDir);
            _pendingQueue = await patcherSvc.BuildQueueAsync(statusProgress);

            if (_pendingQueue.Count == 0)
            {
                SetUI(() =>
                {
                    StatusMessage  = "Up to date";
                    Progress       = 100;
                    ProgressDetail = "";
                    CanPlay        = true;
                    PatchNeeded    = false;
                });
            }
            else
            {
                long totalBytes = _pendingQueue.Sum(j => j.Size);
                SetUI(() =>
                {
                    StatusMessage   = $"{_pendingQueue.Count:N0} file(s) need updating  ({totalBytes / 1024.0 / 1024.0:F0} MB)";
                    PatchButtonText = "PATCH";
                    PatchNeeded     = true;
                });
            }
        }
        catch (Exception ex)
        {
            SetUI(() =>
            {
                StatusMessage = $"Update check failed: {ex.Message}";
                CanPlay       = true;
            });
        }
    }

    // ── Patch on demand ────────────────────────────────────────────────────────

    private async Task StartPatchingAsync()
    {
        if (_pendingQueue == null || _pendingQueue.Count == 0) return;

        var dlProgress = new Progress<DownloadProgress>(p =>
        {
            Progress       = p.Percentage;
            ProgressDetail = $"{p.BytesDownloadedMb} / {p.BytesTotalMb}  ({p.FilesCompleted}/{p.FilesTotal} files)";
            if (!string.IsNullOrEmpty(p.CurrentFile))
                StatusMessage = p.CurrentFile;
        });

        SetUI(() =>
        {
            PatchNeeded     = false;
            IsPatching      = true;
            PatchButtonText = "PATCHING...";
            long totalBytes = _pendingQueue.Sum(j => j.Size);
            StatusMessage   = $"Downloading {_pendingQueue.Count:N0} file(s)  ({totalBytes / 1024.0 / 1024.0:F0} MB)...";
        });

        try
        {
            await new DownloadService(_http).DownloadAllAsync(_pendingQueue, _installDir, dlProgress);

            SetUI(() =>
            {
                StatusMessage  = "Up to date";
                Progress       = 100;
                ProgressDetail = "";
                IsPatching     = false;
                PatchNeeded    = false;
                CanPlay        = true;
            });
        }
        catch (Exception ex)
        {
            SetUI(() =>
            {
                StatusMessage   = $"Patch failed: {ex.Message}";
                IsPatching      = false;
                PatchNeeded     = true;
                PatchButtonText = "RETRY";
            });
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
