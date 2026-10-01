using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Modshift.Services;

/// <summary>
/// Централизованный инфраструктурный сервис для работы с путями файловой системы (Слой Model/Service).
/// </summary>
public static class PathDirectoryService
{
    // Централизованная константа с вашим именем/ником для префикса
    private const string AuthorName = "Hlebushek2020";

    // ИСПРАВЛЕНО: Централизованное свойство для пути к папке профилей
    public static string ProfilesDirectory { get; }

    static PathDirectoryService()
    {
        // 1. Вычисляем и кэшируем корень один раз при старте приложения
        var cachedAppDataPath = InitializeSafeAppDataDirectory();

        // 2. Сразу формируем и кэшируем путь к подпапке профилей
        ProfilesDirectory = Path.Combine(cachedAppDataPath, "profiles");
    }


    private static string InitializeSafeAppDataDirectory()
    {
        try
        {
            string baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (!string.IsNullOrWhiteSpace(baseFolder))
            {
                string targetPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Path.Combine(baseFolder, AuthorName, "Modshift")
                    : Path.Combine(baseFolder, $"{AuthorName}-modshift".ToLowerInvariant());

                Directory.CreateDirectory(targetPath);
                return targetPath;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PathService] Ошибка AppData: {ex.Message}");
        }

        return Path.GetTempPath();
    }
}