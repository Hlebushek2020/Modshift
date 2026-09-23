using CommunityToolkit.Mvvm.ComponentModel;

namespace Modshift.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    // Текущая активная страница (Dashboard, Экран импорта и т.д.)
    [ObservableProperty]
    private ObservableObject _currentPage;

    public MainWindowViewModel()
    {
        // При старте приложения сразу открываем первую страницу — Dashboard
        _currentPage = new DashboardViewModel(this);
    }

    /// <summary>
    /// Метод для смены страниц из любой дочерней ViewModel
    /// </summary>
    public void NavigateTo(ObservableObject nextPage)
    {
        CurrentPage = nextPage;
    }
}