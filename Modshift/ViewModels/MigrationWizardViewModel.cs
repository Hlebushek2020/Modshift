using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Modshift.ViewModels;

public partial class MigrationWizardViewModel : ObservableObject
{
    private readonly AnalysisStepViewModel _step1;
    private readonly ExecutionStepViewModel _step2;
    private int _currentStepIndex = 1;

    [ObservableProperty]
    private ObservableObject _currentStep;

    [ObservableProperty]
    private string _nextButtonText = "НАЧАТЬ ПЕРЕНОС 🚀";

    public bool IsMigrationSuccessful { get; private set; }
    public ProfileCardViewModel? MigratedProfile { get; private set; }

    public MigrationWizardViewModel(string targetVersion, string targetLoader)
    {
        _step1 = new AnalysisStepViewModel(targetVersion, targetLoader);
        _step2 = new ExecutionStepViewModel();
        _currentStep = _step1;
        UpdateWizardState();
    }

    [RelayCommand]
    private void GoNextStep(object? window)
    {
        if (_currentStepIndex == 1)
        {
            _currentStepIndex = 2;
            _currentStep = _step2;
            _step2.StartMigrationProcess();
        }
        else if (_currentStepIndex == 2)
        {
            if (_step2.TotalProgressValue == 100)
            {
                IsMigrationSuccessful = true;
                MigratedProfile = new ProfileCardViewModel
                {
                    Name = $"Миграция: {_step1.TargetVersionOnly} ({_step1.TargetLoaderOnly})",
                    Path = @"C:\Games\Minecraft\profiles\migrated_profile\mods",
                    Version = _step1.TargetVersionOnly,
                    Loader = _step1.TargetLoaderOnly,
                    Description = "Новая изолированная сборка. Оригинальные файлы не изменены."
                };
            }

            if (window is Avalonia.Controls.Window avaloniaWindow)
            {
                avaloniaWindow.Close();
            }
        }

        UpdateWizardState();
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBackStep()
    {
        if (_currentStepIndex == 2)
        {
            _step2.CancelMigration();
            _currentStepIndex = 1;
            _currentStep = _step1;
        }

        UpdateWizardState();
    }

    private bool CanGoBack() => _currentStepIndex == 2;

    [RelayCommand]
    private void CancelWizard(object? window)
    {
        if (_currentStepIndex == 2) _step2.CancelMigration();
        if (window is Avalonia.Controls.Window avaloniaWindow) avaloniaWindow.Close();
    }

    private void UpdateWizardState()
    {
        NextButtonText = _currentStepIndex switch
        {
            1 => "НАЧАТЬ ПЕРЕНОС 🚀",
            2 => "ГОТОВО ✔️",
            _ => "НАЧАТЬ ПЕРЕНОС 🚀"
        };
        GoBackStepCommand.NotifyCanExecuteChanged();
    }
}