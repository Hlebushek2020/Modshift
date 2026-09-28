using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modshift.Models;
using Modshift.Services;
using Modshift.Views;

namespace Modshift.ViewModels;

public partial class WorkspaceViewModel : ObservableObject
{
    private readonly MainWindowViewModel _mainNavigation;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly ProfileCardViewModel _profile;

    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private string _currentVersion = string.Empty;

    public ObservableCollection<string> TargetVersions { get; } = new() { "1.21.1", "1.21", "1.20.4", "1.20.1" };
    public ObservableCollection<string> TargetLoaders { get; } = new() { "Fabric", "Forge", "NeoForge" };

    [ObservableProperty]
    private string _selectedTargetVersion = "1.21.1";

    [ObservableProperty]
    private string _selectedTargetLoader = "Fabric";

    private readonly ObservableCollection<ModTableItemViewModel> _rawMods = new();
    public DataGridCollectionView ModItemsView { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModSelected))]
    private ModTableItemViewModel? _selectedMod;

    public bool IsModSelected => SelectedMod != null;

    public WorkspaceViewModel(
        MainWindowViewModel mainNavigation,
        DashboardViewModel dashboardViewModel,
        ProfileCardViewModel profile)
    {
        _mainNavigation = mainNavigation;
        _dashboardViewModel = dashboardViewModel;
        _profile = profile;
        ProfileName = profile.Name;
        CurrentVersion = profile.Version;

        // Создаем обертку над списком для авто-группировки по имени родителя
        ModItemsView = new DataGridCollectionView(_rawMods);
        ModItemsView.GroupDescriptions.Add(
            new DataGridPathGroupDescription(nameof(ModTableItemViewModel.ParentGroupName)));

        LoadInitialMods();
    }

    private void LoadInitialMods()
    {
        _rawMods.Clear();

        if (_profile.Name.Equals("Сервер Чернигов"))
        {
            _rawMods.Add(
                new ModTableItemViewModel("sodium-fabric-0.5.8.jar", "Основной мод", "Нужен для FPS", "Sodium", true));
            _rawMods.Add(
                new ModTableItemViewModel(
                    "fabric-api-0.92.0.jar",
                    "Системная библиотека",
                    "Авто-определение",
                    "Sodium",
                    false));
            _rawMods.Add(
                new ModTableItemViewModel("iris-mc1.20.1-1.7.0.jar", "Основной мод", "Шейдеры", "Iris Shaders", true));
            _rawMods.Add(
                new ModTableItemViewModel(
                    "cloth-config-11.1.118.jar",
                    "Основной мод",
                    "Локальный файл",
                    "Cloth Config",
                    true));
        }
        else
        {
            foreach (LocalModInfo localModInfo in _profile.Mods)
            {
                _rawMods.Add(
                    new ModTableItemViewModel(
                        localModInfo.DisplayName,
                        "Основной мод",
                        "Локальный файл",
                        localModInfo.ModId,
                        true) { TechnicalModId = localModInfo.ModId });
            }
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _mainNavigation.NavigateTo(_dashboardViewModel);
    }

    /// <summary>
    /// Логика фиолетовой кнопки «ПОЛУЧИТЬ СВЕДЕНИЯ ИЗ API».
    /// Работает прямо на текущей странице без открытия лишних окон.
    /// </summary>
    [RelayCommand]
    private async Task FetchApiDataAsync()
    {
        if (!Directory.Exists(_profile.Path)) return;

        var hashCalculator = new ModHashCalculator();
        var apiService = new ModrinthMigrationService();

        var jarFiles = Directory.GetFiles(_profile.Path, "*.jar");
        var hashesList = new List<string>();
        var hashToModItemMap = new Dictionary<string, ModTableItemViewModel>();

        // 1. Асинхронно вычисляем хэши и связываем их со строками нашей таблицы
        foreach (var jarPath in jarFiles)
        {
            string fileName = Path.GetFileName(jarPath);
            string? hash = await hashCalculator.CalculateSha512Async(jarPath);

            if (!string.IsNullOrEmpty(hash))
            {
                hashesList.Add(hash);
                // Ищем соответствующий элемент в нашей оперативной коллекции таблицы
                var matchedItem = _rawMods.FirstOrDefault(x => x.DisplayName == fileName);
                if (matchedItem != null)
                {
                    hashToModItemMap[hash] = matchedItem;
                }
            }
        }

        // 2. Отправляем ПЕРВЫЙ (массовый) запрос по хэшам
        var apiResults = await apiService.CheckCompatibilityAsync(
            hashesList.ToArray(),
            CurrentVersion,
            _profile.Loader);


        // Список модов, которые Modrinth НЕ узнал по хэшу (моды со сторонних сайтов)
        var unrecognizedMods = new List<ModTableItemViewModel>();

        // Разносим результаты первого этапа
        foreach (var modItem in _rawMods)
        {
            string? fileHash = hashToModItemMap.FirstOrDefault(x => x.Value == modItem).Key;

            if (fileHash != null && apiResults.TryGetValue(fileHash, out var networkStatus))
            {
                // Хэш совпал! Идеальный случай.
                modItem.Comment = networkStatus.StatusText;
            }
            else
            {
                // Хэш НЕ совпал. Отправляем мод на текстовый поиск (План Б)
                unrecognizedMods.Add(modItem);
            }
        }

        // 3. ПЛАН Б: Текстовый поиск по Mod ID для измененных файлов
        foreach (var dirtyMod in unrecognizedMods)
        {
            // Берем чистый технический ID (например, "sodium"), полученный нами из jar-манифеста
            string technicalId = dirtyMod.TechnicalModId; // Добавьте это свойство в модель строки, если его там нет

            if (string.IsNullOrEmpty(technicalId) || technicalId == "unknown")
            {
                dirtyMod.Comment = "⚪ Не опознан локально (Будет скопирован как есть)";
                continue;
            }

            // Делаем точечный запрос по ID проекта на Modrinth
            // Метод CheckCompatibilityBySlugAsync мы добавим в сетевой сервис ниже
            var backupResult = await apiService.CheckCompatibilityBySlugAsync(
                technicalId,
                CurrentVersion,
                _profile.Loader);

            if (backupResult != null)
            {
                // Мод найден по текстовому ID! 
                dirtyMod.Comment = backupResult.StatusText + " ⚠️ (Файл изменен/кастомный)";
            }
            else
            {
                // Если мода вообще нет на Modrinth (эксклюзив с китайских сайтов или RuMinecraft)
                dirtyMod.Comment = "⚪ Отсутствует на Modrinth (Будет скопирован как есть)";
            }
        }

        // 4. Обновляем DataGrid на экране
        ModItemsView.Refresh();
    }

    [RelayCommand]
    private async Task RunMigrationWizardAsync()
    {
        if (App.Current?.ApplicationLifetime is not
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop) return;

        var wizardVm = new MigrationWizardViewModel(SelectedTargetVersion, SelectedTargetLoader);
        var wizardWindow = new MigrationWizardWindow { DataContext = wizardVm };

        await wizardWindow.ShowDialog(desktop.MainWindow!);

        if (wizardVm.IsMigrationSuccessful && wizardVm.MigratedProfile != null)
        {
            _dashboardViewModel.AddNewProfileFromMigration(wizardVm.MigratedProfile);
        }
    }
}

/// <summary>
/// Чистый класс строки таблицы без фоновых генераторов
/// </summary>
public partial class ModTableItemViewModel : ObservableObject
{
    public string DisplayName { get; }
    public string TypeDescription { get; }

    [ObservableProperty]
    private string _comment;

    [ObservableProperty]
    private string _technicalModId;

    public string ParentGroupName { get; }
    public bool IsMainMod { get; }
    public bool IsPinned { get; set; }

    public string Icon => IsMainMod ? "📦" : "🛠️";
    public string ShortComment => Comment.Length > 30 ? Comment[..27] + "..." : Comment;

    public ModTableItemViewModel(
        string displayName,
        string typeDescription,
        string comment,
        string parentGroupName,
        bool isMainMod)
    {
        DisplayName = displayName;
        TypeDescription = typeDescription;
        Comment = comment;
        ParentGroupName = parentGroupName;
        IsMainMod = isMainMod;
    }
}