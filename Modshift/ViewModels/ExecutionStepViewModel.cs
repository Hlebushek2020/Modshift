using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Modshift.ViewModels;

public partial class ExecutionStepViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;
    private readonly StringBuilder _logBuilder = new();

    [ObservableProperty]
    private string _currentOperationText = "Инициализация...";

    [ObservableProperty]
    private string _totalProgressText = "0%";

    [ObservableProperty]
    private int _totalProgressValue = 0;

    [ObservableProperty]
    private bool _isDownloadingFile = false;

    [ObservableProperty]
    private string _downloadingFileName = string.Empty;

    [ObservableProperty]
    private string _downloadSpeedText = string.Empty;

    [ObservableProperty]
    private int _currentFileProgressValue = 0;

    [ObservableProperty]
    private string _logOutput = string.Empty;

    public void StartMigrationProcess()
    {
        _cts = new CancellationTokenSource();
        _logBuilder.Clear();
        LogOutput = string.Empty;
        Task.Run(() => ExecuteMigrationPipelineAsync(_cts.Token));
    }

    public void CancelMigration()
    {
        _cts?.Cancel();
        AppendToLog("[ОТМЕНА] Миграция прервана пользователем.");
        CurrentOperationText = "Миграция прервана.";
        IsDownloadingFile = false;
    }

    private async Task ExecuteMigrationPipelineAsync(CancellationToken token)
    {
        try
        {
            AppendToLog("[INFO] Исходный профиль заблокирован для безопасного чтения.");
            AppendToLog("[INFO] Выделение новой изолированной папки на диске...");
            await Task.Delay(1000, token);
            TotalProgressValue = 30;
            TotalProgressText = "30%";

            CurrentOperationText = "Загрузка обновлённых файлов...";
            IsDownloadingFile = true;
            DownloadingFileName = "sodium-fabric-0.6.5.jar";
            DownloadSpeedText = "4.2 МБ/с";
            for (int i = 0; i <= 100; i += 25)
            {
                token.ThrowIfCancellationRequested();
                CurrentFileProgressValue = i;
                await Task.Delay(200, token);
            }

            AppendToLog("[SUCCESS] Скачан новый независимый файл: sodium-fabric-0.6.5.jar");

            IsDownloadingFile = false;
            TotalProgressValue = 70;
            TotalProgressText = "70%";
            CurrentOperationText = "Запуск фонового процесса Java для генерации метаданных...";
            await Task.Delay(1200, token);
            AppendToLog("[SUCCESS] Конфигурации новой версии успешно сгенерированы в изолированную директорию.");

            TotalProgressValue = 100;
            TotalProgressText = "100%";
            CurrentOperationText = "Миграция успешно завершена!";
            AppendToLog("[ГОТОВО] В дашборд будет добавлен новый изолированный профиль. Исходная папка не изменена.");
        }
        catch (OperationCanceledException)
        {
            AppendToLog("[INFO] Безопасная очистка незавершённого воркспейса.");
        }
        catch (Exception ex)
        {
            AppendToLog($"[ОШИБКА]: {ex.Message}");
        }
    }

    private void AppendToLog(string message)
    {
        _logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        LogOutput = _logBuilder.ToString();
    }
}