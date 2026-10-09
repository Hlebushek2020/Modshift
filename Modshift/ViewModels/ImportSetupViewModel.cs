using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modshift.Models;
using Modshift.Services;

namespace Modshift.ViewModels;

public partial class ImportSetupViewModel : ObservableObject
{
    // Обязательное поле названия. При его изменении Toolkit автоматически перепроверяет, можно ли нажать кнопку импорта
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmImportCommand))]
    private string _packName = string.Empty;

    [ObservableProperty]
    private string _packDescription = string.Empty;

    [ObservableProperty]
    private string _selectedVersion = "1.20.1";

    [ObservableProperty]
    private string _selectedLoader = "Fabric";

    [ObservableProperty]
    private string _modsPath = string.Empty;

    public List<LocalModInfo> _mods;

    // Списки версий и загрузчиков для выпадающих меню (ComboBox)
    public ObservableCollection<string> AvailableVersions { get; } =
        new() { "1.21.1", "1.21", "1.20.4", "1.20.1", "1.19.2" };

    public ObservableCollection<string> AvailableLoaders { get; } = new() { "Fabric", "Forge", "NeoForge" };

    // Плоский список названий .jar файлов для вывода в ListBox
    public ObservableCollection<string> FoundFiles { get; } = new();

    // Флаги результата для Dashboard, чтобы узнать, подтвердил пользователь импорт или отменил
    public bool IsImportConfirmed { get; private set; }
    public ProfileCardViewModel? ResultingProfile { get; private set; }

    public ImportSetupViewModel(string modsPath)
    {
        ModsPath = modsPath;

        // Сразу при открытии окна считываем файлы модов с диска
        LoadLocalFiles();
    }

    /// <summary>
    /// Локально сканирует папку без интернета и вытаскивает только имена файлов
    /// </summary>
    private void LoadLocalFiles()
    {
        if (!Directory.Exists(ModsPath)) return;

        // Вызываем синглтон или новый инстанс сканера Model-слоя
        var scanner = new LocalJarScanner();
        var analysis = scanner.ScanModsFolder(ModsPath);

        // Автоматически выставляем определенные лоадер и версию на экране!
        SelectedVersion = AvailableVersions.Contains(analysis.DetectedVersion)
            ? analysis.DetectedVersion
            : AvailableVersions.FirstOrDefault() ?? "1.20.1-A";

        SelectedLoader = AvailableLoaders.Contains(analysis.DetectedLoader)
            ? analysis.DetectedLoader
            : AvailableLoaders.FirstOrDefault() ?? "Fabric-A";

        // Наполняем плоский список файлов для отображения пользователю
        foreach (var mod in analysis.Mods)
        {
            FoundFiles.Add($"{mod.DisplayName} ({mod.FileName}) — ID: {mod.ModId}");
        }

        _mods = analysis.Mods;
    }

    // Условие активности зеленой кнопки: поле имени не должно быть пустым или состоять из пробелов
    private bool CanConfirmImport() => !string.IsNullOrWhiteSpace(PackName);

    /// <summary>
    /// Команда подтверждения импорта. Вызывается по зеленой кнопке.
    /// Параметр window передается из XAML (ссылка на само окно)
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConfirmImport))]
    private void ConfirmImport(object? window)
    {
        // 1. Создаем уникальный идентификатор для изолированной папки профиля
        var newProfileId = System.Guid.NewGuid();

        // 2. Инициализируем файловый сервис слоя Model
        var configService = new ModpackConfigService();

        // 3. Переносим результаты предварительного сканирования папки в формат сохранения
        var baseModsList = new List<SavedModMetadata>();

        // FoundFiles теперь хранит объекты LocalModInfo, полученные от LocalJarScanner
        foreach (var localMod in _mods)
        {
            baseModsList.Add(
                new SavedModMetadata
                {
                    FileName = localMod.FileName,
                    ModId = localMod.ModId,
                    DisplayName = localMod.DisplayName,
                    ModVersion = localMod.ModVersion,
                    Loader = localMod.Loader,
                    ApiStatusComment = "⚪ Сведения из API ещё не запрашивались",
                    DownloadUrl = null,
                    FileHash = string.Empty, // Хэш пустой, посчитаем его только при первом клике на FetchApiData
                });
        }

        // 4. Формируем полноценную бизнес-модель для сохранения
        var newProfileModel = new ModpackProfile
        {
            Id = newProfileId,
            Name = PackName.Trim(),
            Version = SelectedVersion,
            Loader = SelectedLoader,
            ModsFolderPath = ModsPath,
            Description = PackDescription.Trim(),
            CachedMods = baseModsList // Зашиваем базовый слепок файлов модов
        };

        // 5. Физически создаем папку и пишем profile.json на диск в AppData\Local
        configService.SaveProfile(newProfileModel);

        // 6. Формируем результирующую легкую карточку-черновик для главного экрана Dashboard
        ResultingProfile = new ProfileCardViewModel
        {
            Id = newProfileModel.Id, // Передаем сгенерированный Guid в UI
            Name = newProfileModel.Name,
            Path = newProfileModel.ModsFolderPath,
            Version = newProfileModel.Version,
            Loader = newProfileModel.Loader,
            Description = newProfileModel.Description
        };

        IsImportConfirmed = true;

        if (window is Avalonia.Controls.Window avaloniaWindow)
        {
            avaloniaWindow.Close();
        }
    }

    /// <summary>
    /// Команда отмены импорта. Вызывается по красной кнопке.
    /// </summary>
    [RelayCommand]
    private void CancelImport(object? window)
    {
        IsImportConfirmed = false;

        if (window is Avalonia.Controls.Window avaloniaWindow)
        {
            avaloniaWindow.Close();
        }
    }
}