using System;
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

        var configService = new ModpackConfigService(); // Добавляем сервис конфигураций

        // Читаем текущий снимок профиля с диска, чтобы получить доступ к старому кэшу модов
        var currentProfile = configService.LoadProfileById(_profile.Id);
        if (currentProfile == null) return;


        foreach (SavedModMetadata localModInfo in currentProfile.CachedMods)
        {
            _rawMods.Add(
                new ModTableItemViewModel(
                    localModInfo.FileName,
                    localModInfo.DisplayName,
                    "Основной мод",
                    "Локальный файл",
                    localModInfo.ModId,
                    true) { TechnicalModId = localModInfo.ModId });
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
        var configService = new ModpackConfigService(); // Подключаем сервис для работы с диском

        // ЗАМЕЧАНИЕ 2: Читаем текущий снимок профиля с диска по его Guid
        var currentProfile = configService.LoadProfileById(_profile.Id);
        if (currentProfile == null) return;

        // СВЯЗЫВАНИЕ ПО ID: Строим карту существующего кэша по техническому ID мода: [TechnicalModId] -> [Данные кэша]
        var existingCacheMap = currentProfile.CachedMods
            .Where(m => !string.IsNullOrEmpty(m.ModId))
            .GroupBy(m => m.ModId)
            .ToDictionary(g => g.Key, g => g.First());

        var jarFiles = Directory.GetFiles(_profile.Path, "*.jar");
        var hashesList = new List<string>();

        // Карта связи: [Хэш] -> [Строка таблицы во ViewModel]
        var hashToModItemMap = new Dictionary<string, ModTableItemViewModel>();

        // Временная карта для сохранения вычисленных/взятых из кэша хэшей в рамках этой сессии
        // Карта связи: [TechnicalModId] -> [Вычисленный или взятый из кэша Хэш]
        var fileToHashMap = new Dictionary<string, string>();

        // 1. Асинхронно вычисляем хэши и связываем их со строками нашей таблицы
        foreach (var jarPath in jarFiles)
        {
            string fileName = Path.GetFileName(jarPath);

            // Ищем соответствующий элемент в нашей оперативной коллекции таблицы по имени файла
            var matchedItem = _rawMods.FirstOrDefault(x => x.FileName == fileName);
            if (matchedItem == null) continue;

            string technicalId = matchedItem.TechnicalModId;
            string? hash = null;

            // ОПТИМИЗАЦИЯ: Если этот мод по своему TechnicalModId уже сканировался — НЕ считаем хэш заново!
            if (!string.IsNullOrEmpty(technicalId) && existingCacheMap.TryGetValue(technicalId, out var cachedMod) &&
                !string.IsNullOrEmpty(cachedMod.FileHash))
            {
                hash = cachedMod.FileHash;
            }
            else
            {
                // Мода нет в кэше — вычисляем SHA-512 честно с диска
                hash = await hashCalculator.CalculateSha512Async(jarPath);
            }

            if (!string.IsNullOrEmpty(hash))
            {
                hashesList.Add(hash);

                if (!string.IsNullOrEmpty(technicalId))
                {
                    fileToHashMap[technicalId] = hash; // Запоминаем хэш для последующего сохранения
                }

                hashToModItemMap[hash] = matchedItem;
            }
        }

        // 2. Отправляем ПЕРВЫЙ (массовый) запрос по хэшам (передаем .ToArray() как в вашем оригинале)
        var apiResults = await apiService.CheckCompatibilityAsync(
            hashesList.ToArray(),
            CurrentVersion,
            _profile.Loader);

        // Список модов, которые Modrinth НЕ узнал по хэшу (моды со сторонних сайтов)
        var unrecognizedMods = new List<ModTableItemViewModel>();

        // Карта для сбора прямых ссылок на скачивание, полученных из API
        var modDownloadUrls = new Dictionary<string, string?>();

        // Разносим результаты первого этапа
        foreach (var modItem in _rawMods)
        {
            string? fileHash = hashToModItemMap.FirstOrDefault(x => x.Value == modItem).Key;

            if (fileHash != null && apiResults.TryGetValue(fileHash, out var networkStatus))
            {
                // Хэш совпал! Идеальный случай.
                modItem.Comment = networkStatus.StatusText;

                if (!string.IsNullOrEmpty(modItem.TechnicalModId))
                {
                    modDownloadUrls[modItem.TechnicalModId] = networkStatus.DownloadUrl;
                }
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
            string technicalId = dirtyMod.TechnicalModId;

            if (string.IsNullOrEmpty(technicalId) || technicalId == "unknown")
            {
                dirtyMod.Comment = "⚪ Не опознан локально (Будет скопирован как есть)";
                continue;
            }

            var backupResult = await apiService.CheckCompatibilityBySlugAsync(
                technicalId,
                CurrentVersion,
                _profile.Loader);

            if (backupResult != null)
            {
                // Мод найден по текстовому ID! 
                dirtyMod.Comment = backupResult.StatusText + " ⚠️ (Файл изменен/кастомный)";
                modDownloadUrls[technicalId] = backupResult.DownloadUrl; // Запоминаем ссылку по ID мода
            }
            else
            {
                // Если мода вообще нет на Modrinth
                dirtyMod.Comment = "⚪ Отсутствует на Modrinth (Будет скопирован как есть)";
                modDownloadUrls[technicalId] = null;
            }
        }

        // ====================================================================
        // ЗАМЕЧАНИЕ 1: ПАТТЕРН UPSERT И ФИЗИЧЕСКОЕ СОХРАНЕНИЕ В JSON
        // Наполняем обновленный список модов текущими результатами и пишем на диск
        // ====================================================================
        var updatedModsList = new List<SavedModMetadata>();

        foreach (var modItem in _rawMods)
        {
            string technicalId = modItem.TechnicalModId;

            updatedModsList.Add(
                new SavedModMetadata
                {
                    FileName = modItem.FileName, // Оставляем физическое имя файла для Этапа 2
                    ModId = technicalId, // Главный технический ID ключа
                    DisplayName = modItem.DisplayName,
                    FileHash = !string.IsNullOrEmpty(technicalId)
                        ? fileToHashMap.GetValueOrDefault(technicalId, string.Empty)
                        : string.Empty,
                    ApiStatusComment = modItem.Comment,
                    DownloadUrl = !string.IsNullOrEmpty(technicalId)
                        ? modDownloadUrls.GetValueOrDefault(technicalId)
                        : null
                });
        }

        try
        {
            // Перезаписываем коллекцию и сохраняем обновленный profile.json
            currentProfile.CachedMods = updatedModsList;
            configService.SaveProfile(currentProfile);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] Ошибка сохранения результатов API: {ex.Message}");
        }
        // ====================================================================

        // 4. Обновляем DataGrid на экране
        ModItemsView.Refresh();
    }

    [RelayCommand]
    private async Task RunMigrationWizardAsync()
    {
        if (App.Current?.ApplicationLifetime is not
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop) return;

        var wizardVm = new MigrationWizardViewModel(_profile.Id, SelectedTargetVersion, SelectedTargetLoader);
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
    public string FileName { get; }
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
        string fileName,
        string displayName,
        string typeDescription,
        string comment,
        string parentGroupName,
        bool isMainMod)
    {
        FileName = fileName;
        DisplayName = displayName;
        TypeDescription = typeDescription;
        Comment = comment;
        ParentGroupName = parentGroupName;
        IsMainMod = isMainMod;
    }
}