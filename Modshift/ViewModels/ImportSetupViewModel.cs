using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

        try
        {
            var files = Directory.GetFiles(ModsPath, "*.jar")
                .Select(Path.GetFileName)
                .Where(name => name != null);

            foreach (var file in files)
            {
                FoundFiles.Add(file!);
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modshift] Ошибка локального чтения папки: {ex.Message}");
        }
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
        IsImportConfirmed = true;

        // Формируем карточку, которая вернется в общий список сборок
        ResultingProfile = new ProfileCardViewModel
        {
            Name = PackName.Trim(),
            Path = ModsPath,
            Version = SelectedVersion,
            Loader = SelectedLoader,
            Description = PackDescription.Trim()
        };

        // Закрываем окно через переданный параметр типа Window
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