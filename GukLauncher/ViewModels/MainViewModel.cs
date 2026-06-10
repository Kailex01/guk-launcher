using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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

    // ── Server status ──────────────────────────────────────────────────────────

    private SolidColorBrush _serverDotColor = new(Color.FromRgb(0x6A, 0x6A, 0x6A));
    public SolidColorBrush ServerDotColor
    {
        get => _serverDotColor;
        set { _serverDotColor = value; OnPropertyChanged(); }
    }

    private string _serverStatusText = "Unknown";
    public string ServerStatusText
    {
        get => _serverStatusText;
        set { _serverStatusText = value; OnPropertyChanged(); }
    }

    private string _playerCountText = "";
    public string PlayerCountText
    {
        get => _playerCountText;
        set { _playerCountText = value; OnPropertyChanged(); }
    }

    public ObservableCollection<PatchNote> PatchNotes { get; } = new();
    public ICommand PlayCommand  { get; }
    public ICommand PatchCommand { get; }

    // ── Constructor ────────────────────────────────────────────────────────────

    public MainViewModel()
    {
        _installDir = AppDomain.CurrentDomain.BaseDirectory;
        LogService.Init(_installDir);

        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("GukLauncher/1.0");

        PlayCommand  = new RelayCommand(_ => LaunchGame(),            _ => CanPlay);
        PatchCommand = new RelayCommand(async _ => await StartPatchingAsync(), _ => PatchNeeded && !IsPatching);
    }

    // ── Startup ────────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        LogService.Log("=== Guktown Launcher started ===");
        await Task.WhenAll(LoadPatchNotesAsync(), CheckForUpdatesAsync(), UpdateServerStatusAsync());
        _ = PollServerStatusAsync();
        await CheckForLauncherUpdateAsync();
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
                LogService.Log("Up to date — no files need patching");
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
                LogService.Log($"{_pendingQueue.Count} file(s) need updating ({totalBytes / 1024.0 / 1024.0:F1} MB):");
                foreach (var f in _pendingQueue)
                    LogService.Log($"  needs patch: {f.RelativePath}");
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
            LogService.Log($"Update check failed: {ex.Message}");
            SetUI(() =>
            {
                StatusMessage = $"Update check failed: {ex.Message}";
                CanPlay       = true;
            });
        }
    }

    // ── Launcher self-update ───────────────────────────────────────────────────

    private async Task CheckForLauncherUpdateAsync()
    {
        try
        {
            var svc  = new UpdateService(_http);
            var info = await svc.CheckAsync();
            if (info == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Guktown Launcher {info.TagName} is available.\nInstall now?",
                "Update Available",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            SetUI(() => StatusMessage = $"Downloading update {info.TagName}...");
            await svc.DownloadAndApplyAsync(info);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            LogService.Log($"Launcher update check failed: {ex.Message}");
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

            LogService.Log($"Patch complete — {_pendingQueue.Count} file(s) updated");
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
            LogService.Log($"Patch failed: {ex.Message}");
            SetUI(() =>
            {
                StatusMessage   = $"Patch failed: {ex.Message}";
                IsPatching      = false;
                PatchNeeded     = true;
                PatchButtonText = "RETRY";
            });
        }
    }

    // ── Server status polling ─────────────────────────────────────────────────

    private async Task UpdateServerStatusAsync()
    {
        try
        {
            var status = await new ServerStatusService(_http).FetchAsync()
                .WaitAsync(TimeSpan.FromSeconds(5));
            if (status == null) return;

            SetUI(() =>
            {
                if (status.Online)
                {
                    ServerDotColor    = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // green
                    ServerStatusText  = "Online";
                    PlayerCountText   = status.PlayersOnline == 1
                        ? "1 player online"
                        : $"{status.PlayersOnline} players online";
                }
                else
                {
                    ServerDotColor   = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)); // red
                    ServerStatusText = "Offline";
                    PlayerCountText  = "";
                }
            });
        }
        catch
        {
            SetUI(() =>
            {
                ServerDotColor   = new SolidColorBrush(Color.FromRgb(0x6A, 0x6A, 0x6A)); // gray
                ServerStatusText = "Unknown";
                PlayerCountText  = "";
            });
        }
    }

    private async Task PollServerStatusAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync())
            await UpdateServerStatusAsync();
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
        Application.Current.MainWindow.Hide();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void SetUI(Action action) =>
        Application.Current.Dispatcher.Invoke(action);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
