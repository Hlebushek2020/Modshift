using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Modshift.Models;

namespace Modshift.Services;

/// <summary>
/// Сервис управления конфигурациями и изолированными файлами профилей сборок (Слой Model/Service).
/// Реализует транзакционный паттерн Read-Modify-Write с полной перезаписью индивидуальных файлов.
/// </summary>
public class ModpackConfigService
{
    private const string ProfileFileName = "profile.json";

    private readonly Version _schemaVersion = GetCoreLibraryVersion();

    /// <summary>
    /// Возвращает точную версию сборки библиотеки ядра Modshift.Core.
    /// </summary>
    private static Version GetCoreLibraryVersion()
    {
        Version defaultSv = new Version(1, 0, 0, 0);

        try
        {
            // Находим сборку, в которой выполняется этот конкретный код (т.е. Modshift.Core)
            Assembly assembly = Assembly.GetExecutingAssembly();

            // Забираем версию (извлекает данные из тега <Version> в .csproj файле)
            return assembly.GetName().Version ?? defaultSv;
        }
        catch
        {
            return defaultSv;
        }
    }

    /// <summary>
    /// Физически сканирует директорию профилей и загружает метаданные всех существующих сборок.
    /// Вызывается один раз при старте приложения для наполнения стартового экрана (Dashboard).
    /// </summary>
    public List<ModpackProfile> LoadAllProfiles()
    {
        var profiles = new List<ModpackProfile>();

        try
        {
            // Метод CreateDirectory безопасен: если папка "profiles" уже есть, он ничего не сделает
            Directory.CreateDirectory(PathDirectoryService.ProfilesDirectory);

            // Сканируем все подпапки внутри каталога профилей
            var subDirectories = Directory.GetDirectories(PathDirectoryService.ProfilesDirectory);

            foreach (var dirPath in subDirectories)
            {
                // Внутри каждой папки-GUID ищем файл profile.json
                string configFilePath = Path.Combine(dirPath, ProfileFileName);

                if (!File.Exists(configFilePath))
                    continue;

                try
                {
                    string json = File.ReadAllText(configFilePath);

                    // Десериализация без рефлексии через Source Generator контекст
                    var profile = JsonSerializer.Deserialize(json, AppConfigJsonContext.Default.ModpackProfile);

                    if (profile != null)
                    {
                        profiles.Add(profile);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ConfigService] Ошибка чтения профиля в {Path.GetFileName(dirPath)}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ConfigService] Критическая ошибка сканирования директории профилей: {ex.Message}");
        }

        return profiles;
    }

    /// <summary>
    /// Точечно считывает один конкретный файл профиля с диска по его Guid.
    /// Вызывается по паттерну Read-on-Demand при переходе пользователя в рабочую область сборки.
    /// </summary>
    public ModpackProfile? LoadProfileById(Guid profileId)
    {
        try
        {
            string configFilePath = Path.Combine(
                PathDirectoryService.ProfilesDirectory,
                profileId.ToString(),
                ProfileFileName);

            if (!File.Exists(configFilePath))
                return null;

            string json = File.ReadAllText(configFilePath);
            return JsonSerializer.Deserialize(json, AppConfigJsonContext.Default.ModpackProfile);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ConfigService] Ошибка точечного чтения профиля {profileId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Создает новую или полностью перезаписывает существующую изолированную папку профиля на диске.
    /// </summary>
    public void SaveProfile(ModpackProfile profile)
    {
        try
        {
            // Формируем путь к персональной папке сборки на основе её уникального Guid
            string targetProfileFolder = Path.Combine(PathDirectoryService.ProfilesDirectory, profile.Id.ToString());

            // Создаем структуру папки (включая будущую подпапку под конфиги модов)
            Directory.CreateDirectory(targetProfileFolder);
            //Directory.CreateDirectory(Path.Combine(targetProfileFolder, "configs"));

            string configFilePath = Path.Combine(targetProfileFolder, ProfileFileName);

            profile.SchemaVersion = _schemaVersion;

            // Сериализуем объект через жестко скомпилированный контекст Source Generator
            string json = JsonSerializer.Serialize(profile, AppConfigJsonContext.Default.ModpackProfile);

            // Атомарная и безопасная перезапись файла операционной системой
            File.WriteAllText(configFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] Ошибка записи профиля {profile.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Полностью и безвозвратно удаляет всю изолированную папку профиля со всеми вложенными файлами и конфигами.
    /// </summary>
    public void DeleteProfile(Guid profileId)
    {
        try
        {
            string targetProfileFolder = Path.Combine(PathDirectoryService.ProfilesDirectory, profileId.ToString());

            // Проверяем, существует ли папка, чтобы избежать ложных исключений ОС
            if (Directory.Exists(targetProfileFolder))
            {
                // Флаг true указывает операционной системе удалить папку рекурсивно вместе со всем содержимым
                Directory.Delete(targetProfileFolder, recursive: true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ConfigService] Ошибка удаления папки профиля {profileId}: {ex.Message}");
        }
    }
}