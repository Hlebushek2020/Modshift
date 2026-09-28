using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Modrinth;
using Modrinth.Models.Enums;

namespace Modshift.Services;

public class ModrinthMigrationService
{
    private readonly ModrinthClient _client;

    public ModrinthMigrationService()
    {
        // 1. Динамически извлекаем метаданные прямо из запущенной сборки
        var assembly = Assembly.GetExecutingAssembly();

        // Очищаем имя от спецсимволов и пробелов, которые Cloudflare может счесть атакой
        string rawAppName = assembly.GetName().Name ?? "Modshift";
        string appName = new string(rawAppName.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());

        // Получаем текущую версию (например, "1.0.0.0")
        string appVersion = assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        // Ссылку на GitHub мы можем безопасно вытащить из атрибута AssemblyDescription или AssemblyProduct,
        // но так как репозиторий — это внешняя ссылка, самым надежным решением для .NET 8/9 является 
        // чтение кастомного свойства метаданных мета-тега репозитория сборки.
        // Если его нет, ставим безопасный fallback, чтобы API нас не заблокировало.
        string repositoryUrl = "https://github.com/Hlebushek2020/Modshift";

        // 1. Инициализируем конфигурацию клиента по канонам библиотеки
        var config = new ModrinthClientConfig
        {
            // Указываем User-Agent, библиотека сама обернет его в правильные HTTP-обертки
            UserAgent = $"{appName}/{appVersion} ({repositoryUrl})"
        };

        _client = new ModrinthClient(config);
    }

    /// <summary>
    /// Пакетный поиск модов по SHA-512 хэшам (Основной конвейер)
    /// </summary>
    public async Task<Dictionary<string, ApiCheckResult>> CheckCompatibilityAsync(
        string[] hashes,
        string targetVersion,
        string targetLoader,
        CancellationToken token = default)
    {
        var resultGrid = new Dictionary<string, ApiCheckResult>();
        if (hashes.Length == 0) return resultGrid;

        try
        {
            // Библиотека сама отправляет пачку хэшей одним POST-запросом
            var filesMap = await _client.VersionFile.GetMultipleVersionsByHashAsync(hashes, HashAlgorithm.Sha512);
            if (filesMap == null) return resultGrid;

            foreach (var kvp in filesMap)
            {
                token.ThrowIfCancellationRequested();
                string fileHash = kvp.Key;
                var fileInfo = kvp.Value;

                // Запрашиваем версии этого проекта под новый патч игры
                var versions = await _client.Version.GetProjectVersionListAsync(fileInfo.ProjectId);

                bool versionFound = false;
                foreach (var version in versions)
                {
                    // Проверяем, подходит ли версия под целевой Minecraft и лоадер
                    if (version.GameVersions.Contains(targetVersion) &&
                        version.Loaders.Contains(targetLoader.ToLowerInvariant()))
                    {
                        resultGrid[fileHash] = new ApiCheckResult(
                            IsFound: true,
                            StatusText: $"🟢 Доступно: v{version.VersionNumber}",
                            DownloadUrl: version.Files?[0]?.Url // Берем ссылку на первый .jar файл
                        );
                        versionFound = true;
                        break;
                    }
                }

                if (!versionFound)
                {
                    resultGrid[fileHash] = new ApiCheckResult(
                        IsFound: true,
                        StatusText: "❌ Нет версии под указанные параметры",
                        DownloadUrl: null);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth.Net Error] Ошибка по хэшам: {ex.Message}");
        }

        return resultGrid;
    }

    /// <summary>
    /// ПЛАН Б: Безопасный текстовый поиск через поисковый движок библиотеки (Защита от ошибки 10054)
    /// </summary>
    public async Task<ApiCheckResult?> CheckCompatibilityBySlugAsync(
        string modId,
        string targetVersion,
        string targetLoader,
        CancellationToken token = default)
    {
        try
        {
            //var searchResult = await _client.Project.SearchAsync(modId);
            //if (searchResult.Hits.Length == 0) return null;

            // Берем самый релевантный первый результат
            //var firstHit = searchResult.Hits[0];

            // Запрашиваем его версии
            var versions = await _client.Version.GetProjectVersionListAsync(
                modId,
                new[] { targetLoader.ToLower() },
                new[] { targetVersion });

            foreach (var version in versions)
            {
                if (version.GameVersions.Contains(targetVersion) &&
                    version.Loaders.Contains(targetLoader.ToLowerInvariant()))
                {
                    return new ApiCheckResult(
                        IsFound: true,
                        StatusText: $"🟢 Доступно: v{version.VersionNumber}",
                        DownloadUrl: version.Files?[0]?.Url);
                }
            }

            return new ApiCheckResult(
                IsFound: true,
                StatusText: "❌ Нет версии под указанные параметры",
                DownloadUrl: null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth.Net Error] Ошибка поиска по ID {modId}: {ex.Message}");
            return null;
        }
    }
}

// --- ВСПОМОГАТЕЛЬНЫЕ СТРУКТУРЫ И СГЕНЕРИРОВАННЫЙ КОНТЕКСТ JSON ---

public record ApiCheckResult(bool IsFound, string StatusText, string? DownloadUrl);

internal class HashesRequest
{
    [JsonPropertyName("hashes")]
    public List<string> Hashes { get; set; } = new();

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "sha512";
}

internal class ModrinthVersionFile
{
    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string VersionId { get; set; } = string.Empty;
}

internal class ModrinthVersionInfo
{
    [JsonPropertyName("version_number")]
    public string VersionNumber { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public List<ModrinthFileLink>? Files { get; set; }
}

internal class ModrinthFileLink
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

// Один атрибут на корень для Source Generator. Он сам рекурсивно разберет все вложенные списки и словари!
[JsonSerializable(typeof(HashesRequest))]
[JsonSerializable(typeof(Dictionary<string, ModrinthVersionFile>))]
[JsonSerializable(typeof(List<ModrinthVersionInfo>))]
internal partial class MigrationJsonContext : JsonSerializerContext
{
}