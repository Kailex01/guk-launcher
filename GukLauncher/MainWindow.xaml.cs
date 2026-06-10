using System.Windows;
using GukLauncher.ViewModels;

namespace GukLauncher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.InitializeAsync();
    }

    private void TitleBar_Drag(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => DragMove();

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();
}
