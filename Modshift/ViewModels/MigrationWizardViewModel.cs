using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modshift.Models;
using Modshift.Services;

namespace Modshift.ViewModels;

public partial class MigrationWizardViewModel : ObservableObject
{
    private readonly Guid _sourceProfileId;
    private readonly AnalysisStepViewModel _step1;
    private readonly ExecutionStepViewModel _step2;
    private int _currentStepIndex = 1;

    [ObservableProperty]
    private ObservableObject _currentStep;

    [ObservableProperty]
    private string _nextButtonText = "НАЧАТЬ ПЕРЕНОС 🚀";

    public bool IsMigrationSuccessful { get; private set; }
    public ProfileCardViewModel? MigratedProfile { get; private set; }

    public MigrationWizardViewModel(Guid sourceProfileId, string targetVersion, string targetLoader)
    {
        _sourceProfileId = sourceProfileId;
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
                // 1. Инициализируем сервис профилей и генерируем новый Guid для мигрированной сборки
                var configService = new ModpackConfigService();

                var migratedModsList = new List<SavedModMetadata>();

                // ====================================================================
                // 🛠️ СЛИЯНИЕ ИЗ КЭША ДИСКА (ЧИТАЕМ СТАРЫЙ ПРОФИЛЬ ПО ID)
                // Точечно загружаем исходный профиль с диска по переданному Guid
                // ====================================================================
                var sourceProfileData = configService.LoadProfileById(_sourceProfileId);

                if (sourceProfileData != null)
                {
                    // TODO: ЗАГЛУШКА ДЛЯ ЭТАПА 1: Проходим по старому кэшу и дублируем его структуру
                    foreach (var oldMod in sourceProfileData.CachedMods)
                    {
                        migratedModsList.Add(
                            new SavedModMetadata
                            {
                                FileName = oldMod.FileName,
                                ModId = oldMod.ModId,
                                DisplayName = oldMod.DisplayName,
                                ModVersion = oldMod.ModVersion,
                                Loader = _step1.TargetLoaderOnly, // Новый лоадер
                                FileHash = oldMod.FileHash, // Сохраняем вычисленный хэш
                                ApiStatusComment = $"🟢 [ЗАГЛУШКА MVP] Перенесено на {_step1.TargetVersionOnly}",
                                DownloadUrl = oldMod.DownloadUrl
                            });
                    }
                }
                // ====================================================================


                // 2. Формируем итоговую бизнес-модель нового мигрированного профиля
                var migratedProfileModel = new ModpackProfile
                {
                    Name = $"Миграция: {_step1.TargetVersionOnly} ({_step1.TargetLoaderOnly})",
                    Version = _step1.TargetVersionOnly,
                    Loader = _step1.TargetLoaderOnly,
                    ModsFolderPath = @"C:\Games\Minecraft\profiles\migrated_profile\mods",
                    Description = "Новая изолированная сборка. Оригинальные файлы не изменены.",
                    CachedMods = migratedModsList // Записываем сдублированный список
                };

                // 3. Физически пишем profile.json на диск в AppData\Local через Source Generator
                configService.SaveProfile(migratedProfileModel);

                // 4. Заполняем свойства вашей оригинальной UI-карточки
                IsMigrationSuccessful = true;
                MigratedProfile = new ProfileCardViewModel
                {
                    Id = migratedProfileModel.Id, // Привязываем Guid для будущих CRUD-операций
                    Name = migratedProfileModel.Name,
                    Path = migratedProfileModel.ModsFolderPath,
                    Version = migratedProfileModel.Version,
                    Loader = migratedProfileModel.Loader,
                    Description = migratedProfileModel.Description
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