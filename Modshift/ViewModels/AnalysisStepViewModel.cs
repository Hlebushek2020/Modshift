using System.Collections.ObjectModel;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Modshift.ViewModels;

public partial class AnalysisStepViewModel : ObservableObject
{
    [ObservableProperty]
    private string _targetInfo = string.Empty;

    public string TargetVersionOnly { get; }
    public string TargetLoaderOnly { get; }

    private readonly ObservableCollection<WizardModItemViewModel> _compatibilityList = new();
    public DataGridCollectionView CompatibilityView { get; }

    public AnalysisStepViewModel(string targetVersion, string targetLoader)
    {
        TargetVersionOnly = targetVersion;
        TargetLoaderOnly = targetLoader;
        TargetInfo = $"Minecraft {targetVersion} ({targetLoader})";

        CompatibilityView = new DataGridCollectionView(_compatibilityList);
        CompatibilityView.GroupDescriptions.Add(
            new DataGridPathGroupDescription(nameof(WizardModItemViewModel.ParentGroupName)));

        LoadAnalysisMock();
    }

    private void LoadAnalysisMock()
    {
        _compatibilityList.Clear();
        _compatibilityList.Add(
            new WizardModItemViewModel(
                "sodium-fabric-0.5.8.jar",
                "Основной мод",
                "🟢 Найдена версия v0.6.5",
                "Sodium",
                true));
        _compatibilityList.Add(
            new WizardModItemViewModel(
                "fabric-api-0.92.0.jar",
                "Зависимость",
                "⚡ Скачается авто-апдейт",
                "Sodium",
                false));
        _compatibilityList.Add(
            new WizardModItemViewModel(
                "rough-ly-enough-11.1.jar",
                "Основной мод",
                "❌ Нет версии под указанные параметры",
                "Rough Ly Enough",
                true));
    }
}

public class WizardModItemViewModel
{
    public string DisplayName { get; }
    public string TypeDescription { get; }
    public string StatusResult { get; }
    public string ParentGroupName { get; }
    public bool IsMainMod { get; }

    public string Icon => IsMainMod ? "📦" : "🛠️";

    public string StatusColorBrush => StatusResult.StartsWith("🟢") ? "#4CAF50" :
        StatusResult.StartsWith("⚡") ? "#2196F3" : "#F44336";

    public WizardModItemViewModel(
        string displayName,
        string typeDescription,
        string statusResult,
        string parentGroupName,
        bool isMainMod)
    {
        DisplayName = displayName;
        TypeDescription = typeDescription;
        StatusResult = statusResult;
        ParentGroupName = parentGroupName;
        IsMainMod = isMainMod;
    }
}