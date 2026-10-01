using System;
using System.Collections.Generic;
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
using Modshift.Models;
using Modshift.Services;
using Modshift.Views;

namespace Modshift.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly MainWindowViewModel _mainNavigation;
    private readonly ModpackConfigService _configService;

    // Список карточек сборок для отображения в ListBox
    public ObservableCollection<ProfileCardViewModel> SavedProfiles { get; } = new();

    public ICommand DeleteProfileCommand { get; }

    public DashboardViewModel(MainWindowViewModel mainNavigation)
    {
        _mainNavigation = mainNavigation;
        _configService = new ModpackConfigService();

        DeleteProfileCommand = new RelayCommand<ProfileCardViewModel>(DeleteProfile);

        // В реальном коде здесь будет вызов загрузки из app_config.json через ModpackConfigService
        LoadSavedProfilesMock();
    }


    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow == null) return;

        var settingsVm = new SettingsViewModel();
        var settingsWindow = new SettingsWindow
        {
            DataContext = settingsVm
        };

        // Открываем модально поверх главного окна
        await settingsWindow.ShowDialog(desktop.MainWindow);
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
            AddNewProfile(importViewModel.ResultingProfile);
        }
    }

    private void RefreshProfiles(ProfileCardViewModel? newProfile)
    {
        if (newProfile != null)
        {
            SavedProfiles.Add(newProfile);
        }
    }

    [RelayCommand]
    private void OpenProfile(ProfileCardViewModel? profile)
    {
        if (profile == null) return;

        // 1. Создаем ViewModel для рабочего пространства и передаем туда данные выбранного профиля
        var workspaceVm = new WorkspaceViewModel(_mainNavigation, this, profile);

        // 2. Даем команду Главному окну переключить центральную область на этот экран
        _mainNavigation.NavigateTo(workspaceVm);
    }

    private void DeleteProfile(ProfileCardViewModel? profile)
    {
        if (profile == null) return;

        // Удаляем файл с диска через сервис
        _configService.DeleteProfile(profile.Id);

        // Удаляем только из списка программы (из app_config.json), файлы на диске не трогаем
        SavedProfiles.Remove(profile);
    }

    private void AddNewProfile(ProfileCardViewModel newProfile)
    {
        // 1. Если у карточки еще нет ID (например, только что создана в окне импорта), генерируем его
        if (newProfile.Id == Guid.Empty)
        {
            newProfile.Id = Guid.NewGuid();
        }

        // 2. Переносим данные из ViewModel в чистую бизнес-модель для сохранения
        var modelToSave = new ModpackProfile
        {
            Id = newProfile.Id,
            Name = newProfile.Name,
            ModsFolderPath = newProfile.Path,
            Version = newProfile.Version,
            Loader = newProfile.Loader,
            Description = newProfile.Description
        };

        // 3. Просим сервис физически создать отдельный .json файл на диске
        _configService.SaveProfile(modelToSave);

        // 1. Добавляем в глобальный список (XAML Dashboard сразу увидит ее)
        RefreshProfiles(newProfile);
    }

    /// <summary>
    /// Регистрирует новую сборку, созданную в результате миграции
    /// </summary>
    public void AddNewProfileFromMigration(ProfileCardViewModel newProfile)
    {
        AddNewProfile(newProfile);

        // 3. Автоматически возвращаем пользователя на начальный экран, чтобы он увидел результат
        _mainNavigation.NavigateTo(this);
    }

    private void LoadSavedProfilesMock()
    {
        // 3. Загружаем раздельные профили с диска и переносим их во ViewModel карточек
        var localProfiles = _configService.LoadAllProfiles();
        foreach (var profile in localProfiles)
        {
            SavedProfiles.Add(
                new ProfileCardViewModel
                {
                    Id = profile.Id, // Обязательно сохраняем Guid для будущих операций удаления/открытия
                    Name = profile.Name,
                    Path = profile.ModsFolderPath,
                    Version = profile.Version,
                    Loader = profile.Loader,
                    Description = profile.Description
                });
        }

        /*
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
        */
    }
}

/// <summary>
/// Маленькая ViewModel чисто под данные одной карточки профиля
/// </summary>
public partial class ProfileCardViewModel : ObservableObject
{
    public Guid Id { get; set; }

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

    [ObservableProperty]
    private List<LocalModInfo> _mods;
}