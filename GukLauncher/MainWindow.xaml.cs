using System.Windows;
using GukLauncher.ViewModels;

namespace GukLauncher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private System.Windows.Forms.NotifyIcon _trayIcon = null!;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        InitTrayIcon();
        Loaded += async (_, _) => await _vm.InitializeAsync();
    }

    private void InitTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon { Text = "Guktown Launcher", Visible = true };

        var iconStream = Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/GukTown.ico"))?.Stream;
        if (iconStream != null)
            _trayIcon.Icon = new System.Drawing.Icon(iconStream);

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open Launcher", null, (_, _) => ShowWindow());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowWindow();
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosed(EventArgs e)
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.OnClosed(e);
    }

    private void TitleBar_Drag(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => DragMove();

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => Hide();

    private void Close_Click(object sender, RoutedEventArgs e)
        => ExitApp();
}
