using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Modshift.ViewModels;

public partial class WorkspaceViewModel : ObservableObject
{
    private readonly MainWindowViewModel _mainNavigation;

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

    public WorkspaceViewModel(MainWindowViewModel mainNavigation, ProfileCardViewModel profile)
    {
        _mainNavigation = mainNavigation;
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

    [RelayCommand]
    private void GoBack()
    {
        _mainNavigation.NavigateTo(new DashboardViewModel(_mainNavigation));
    }

    [RelayCommand]
    private async Task FetchApiDataAsync()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RunMigrationWizardAsync()
    {
        await Task.CompletedTask;
    }
}

/// <summary>
/// Чистый класс строки таблицы без фоновых генераторов
/// </summary>
public class ModTableItemViewModel
{
    public string DisplayName { get; }
    public string TypeDescription { get; }
    public string Comment { get; }
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