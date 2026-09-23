using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modshift.Views;

namespace Modshift.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly MainWindowViewModel _mainNavigation;

    // Список карточек сборок для отображения в ListBox
    public ObservableCollection<ProfileCardViewModel> SavedProfiles { get; } = new();

    public ICommand OpenProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }

    public DashboardViewModel(MainWindowViewModel mainNavigation)
    {
        _mainNavigation = mainNavigation;

        OpenProfileCommand = new RelayCommand<ProfileCardViewModel>(OpenProfile);
        DeleteProfileCommand = new RelayCommand<ProfileCardViewModel>(DeleteProfile);

        // В реальном коде здесь будет вызов загрузки из app_config.json через ModpackConfigService
        LoadSavedProfilesMock();
    }

    [RelayCommand]
    private async Task OpenImportWindowAsync()
    {
        // 1. Получаем ссылку на главное физическое окно приложения для вызова проводника
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow == null) return;

        // 2. Настраиваем и открываем системный диалог выбора папки
        var options = new FolderPickerOpenOptions
        {
            Title = "Выберите папку mods вашей сборки",
            AllowMultiple = false
        };

        var result = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(options);
        var selectedFolder = result.FirstOrDefault();

        if (selectedFolder == null) return; // Пользователь закрыл проводник

        // Получаем локальный абсолютный путь к папке
        string modsFolderPath = selectedFolder.Path.LocalPath;

        // Проверяем, что папка называется именно "mods" (базовая защита от дурака)
        //if (Path.GetFileName(modsFolderPath).ToLower() != "mods")
        //{
        //    // Здесь в будущем можно выводить MessageBox, пока просто выходим
        //    return;
        //}

        // 3. Создаем ViewModel для окна импорта и передаем туда выбранный путь
        var importViewModel = new ImportSetupViewModel(modsFolderPath);

        // 4. Открываем отдельное физическое окно (код окна ниже)
        // Так как запуск окон — это задача View, мы можем сделать это через рефлексию или прямой вызов, 
        // что допустимо в событии команды жизненного цикла окна.
        var importWindow = new ImportSetupWindow { DataContext = importViewModel };

        // Ждем пока пользователь закроет окно импорта
        await importWindow.ShowDialog(desktop.MainWindow);

        // 5. Если пользователь успешно импортировал сборку (флаг внутри importViewModel)
        if (importViewModel.IsImportConfirmed)
        {
            // Перезагружаем список профилей на Dashboard
            RefreshProfiles(importViewModel.ResultingProfile);
        }
    }

    private void RefreshProfiles(ProfileCardViewModel? newProfile)
    {
        if (newProfile != null)
        {
            SavedProfiles.Add(newProfile);
        }
    }

    private void OpenProfile(ProfileCardViewModel? profile)
    {
        if (profile == null) return;

        // Переключаем центральную область главного окна на рабочий экран выбранной сборки (WorkspaceView)
        // _mainNavigation.NavigateTo(new WorkspaceViewModel(_mainNavigation, profile.Path));
    }

    private void DeleteProfile(ProfileCardViewModel? profile)
    {
        if (profile == null) return;

        // Удаляем только из списка программы (из app_config.json), файлы на диске не трогаем
        SavedProfiles.Remove(profile);
    }

    private void LoadSavedProfilesMock()
    {
        // Временная заглушка данных для визуализации в GUI
        SavedProfiles.Add(
            new ProfileCardViewModel
            {
                Name = "Сервер Чернигов",
                Loader = "Fabric",
                Version = "1.20.1",
                Path = @"C:\Games\Minecraft\profiles\server_1\mods",
                Description = "Основная сборка для друзей. Оптимизация и кастомные шейдеры."
            });
    }
}

/// <summary>
/// Маленькая ViewModel чисто под данные одной карточки профиля
/// </summary>
public partial class ProfileCardViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _loader = string.Empty;

    [ObservableProperty]
    private string _version = string.Empty;

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;
}