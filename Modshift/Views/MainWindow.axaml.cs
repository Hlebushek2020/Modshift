using Avalonia.Controls;
using Modshift.ViewModels;

namespace Modshift.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}