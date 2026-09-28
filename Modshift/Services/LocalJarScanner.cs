using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Modshift.Models;
using Tomlyn;
using Tomlyn.Model;

namespace Modshift.Services;

public class LocalJarScanner
{
    /// <summary>
    /// Сканирует папку mods, извлекает зависимости и определяет версию игры по мажоритарному признаку.
    /// </summary>
    public LocalAnalysisResult ScanModsFolder(string modsFolderPath)
    {
        var result = new LocalAnalysisResult();
        if (!Directory.Exists(modsFolderPath)) return result;

        // Сортируем все файлы .jar по алфавиту
        var jarFiles = Directory.GetFiles(modsFolderPath, "*.jar")
            .OrderBy(Path.GetFileName)
            .ToArray();

        var versionVotes = new Dictionary<string, int>();
        var loaderVotes = new Dictionary<string, int>();

        foreach (var jarPath in jarFiles)
        {
            string fileName = Path.GetFileName(jarPath);
            LocalModInfo? modInfo = null;

            try
            {
                using var archive = ZipFile.OpenRead(jarPath);

                // 1. Проверяем Fabric
                var fabricEntry = archive.GetEntry("fabric.mod.json");
                if (fabricEntry != null)
                {
                    using var stream = fabricEntry.Open();
                    modInfo = ParseFabricModJson(stream, fileName);
                    if (modInfo != null)
                    {
                        IncrementVote(loaderVotes, "Fabric");
                    }
                }

                // 2. Если не Fabric, проверяем Forge / NeoForge
                if (modInfo == null)
                {
                    var forgeEntry = archive.GetEntry("META-INF/mods.toml");
                    if (forgeEntry != null)
                    {
                        using var stream = forgeEntry.Open();
                        modInfo = ParseForgeModsToml(stream, fileName);
                        if (modInfo != null)
                        {
                            IncrementVote(loaderVotes, modInfo.Loader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Scanner Warning] Ошибка чтения архива {fileName}: {ex.Message}");
                // Ловушка для битых, заблокированных или пустых zip-файлов — мы их не выбрасываем!
            }

            // ====================================================================
            // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Безопасная заглушка (Fallback)
            // Если файла нет в списке (modInfo == null) из-за отсутствия манифестов
            // или ошибки чтения архива, мы ВСЕ РАВНО добавляем его в список!
            // ====================================================================
            if (modInfo == null)
            {
                modInfo = new LocalModInfo
                {
                    FileName = fileName,
                    ModId = Path.GetFileNameWithoutExtension(fileName).ToLower().Replace(" ", "-"),
                    DisplayName = fileName, // Выводим полное имя файла как название
                    ModVersion = "Unknown",
                    Loader = "Unknown"
                };
            }

            // Подсчитываем голоса за версию игры ТОЛЬКО если она была успешно найдена внутри мода
            foreach (var ver in modInfo.LocalDependencies.Where(d => d.StartsWith("mc_ver:")))
            {
                IncrementVote(versionVotes, ver.Replace("mc_ver:", ""));
            }

            // Очищаем префиксы версий майнкрафта, оставляя чистые ID модов-зависимостей
            modInfo.LocalDependencies = modInfo.LocalDependencies.Where(d => !d.StartsWith("mc_ver:")).ToList();

            // Гарантированно добавляем файл в итоговый результат
            result.Mods.Add(modInfo);
        }

        // Вычисляем мажоритарную (самую частую) версию игры и лоадер
        if (versionVotes.Any())
            result.DetectedVersion = versionVotes.OrderByDescending(x => x.Value).First().Key;

        if (loaderVotes.Any())
            result.DetectedLoader = loaderVotes.OrderByDescending(x => x.Value).First().Key;
        else
            result.DetectedLoader =
                "Fabric"; // Дефолтный безопасный фолбек для комбобокса, если папка пуста или неопознана

        return result;
    }

    private LocalModInfo? ParseFabricModJson(Stream stream, string fileName)
    {
        try
        {
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            var info = new LocalModInfo
            {
                FileName = fileName,
                ModId = root.GetProperty("id").GetString() ?? string.Empty,
                DisplayName = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : fileName,
                ModVersion = root.GetProperty("version").GetString() ?? "1.0.0",
                Loader = "Fabric"
            };

            // Читаем зависимости Fabric
            if (root.TryGetProperty("depends", out var depends))
            {
                foreach (var prop in depends.EnumerateObject())
                {
                    if (prop.Name == "minecraft")
                    {
                        // Временно маркируем префиксом, чтобы вытащить версию выше
                        string rawVersion = CleanVersionString(prop.Value.GetString() ?? "");
                        if (!string.IsNullOrEmpty(rawVersion))
                            info.LocalDependencies.Add($"mc_ver:{rawVersion}");
                    }
                    else
                    {
                        info.LocalDependencies.Add(prop.Name);
                    }
                }
            }

            return info;
        }
        catch
        {
            return null;
        }
    }

    private LocalModInfo? ParseForgeModsToml(Stream stream, string fileName)
    {
        try
        {
            using var reader = new StreamReader(stream);
            string tomlContent = reader.ReadToEnd();

            // ====================================================================
            // ИСПРАВЛЕНО: Начиная с версии 0.17+, вместо старого Toml.ToModel 
            // мы используем нативный статический метод TomlTable.FromText
            // ====================================================================
            TomlTable tomlModel = TomlSerializer.Deserialize<TomlTable>(tomlContent);

            // Дальнейший код извлечения данных из таблиц остается точно таким же:
            if (!tomlModel.TryGetValue("mods", out var modsObj) || modsObj is not TomlTableArray modsArray ||
                modsArray.Count == 0)
                return null;

            // В Tomlyn массивы таблиц возвращаются как TomlTable внутри TomlArray
            if (modsArray[0] is not TomlTable mainModTable)
                return null;

            string modId = mainModTable.TryGetValue("modId", out var idObj) ? idObj.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(modId)) return null;

            var info = new LocalModInfo
            {
                FileName = fileName,
                ModId = modId,
                DisplayName = mainModTable.TryGetValue("displayName", out var nameObj)
                    ? nameObj.ToString() ?? modId
                    : modId,
                ModVersion = mainModTable.TryGetValue("version", out var verObj)
                    ? verObj.ToString() ?? "1.0.0"
                    : "1.0.0",
                Loader = tomlContent.Contains("neoforge") ? "NeoForge" : "Forge"
            };

            // Извлекаем зависимости
            if (tomlModel.TryGetValue("dependencies", out var depsObj) && depsObj is TomlTable depsTable)
            {
                if (depsTable.TryGetValue(modId, out var modDepsObj) && modDepsObj is TomlTableArray modDepsArray)
                {
                    foreach (var depItem in modDepsArray)
                    {
                        if (depItem is not TomlTable depTable) continue;

                        if (depTable.TryGetValue("modId", out var depModIdObj))
                        {
                            string depModId = depModIdObj.ToString() ?? "";

                            if (depModId == "minecraft")
                            {
                                if (depTable.TryGetValue("versionRange", out var rangeObj))
                                {
                                    string cleanVer = CleanVersionString(rangeObj.ToString() ?? "");
                                    if (!string.IsNullOrEmpty(cleanVer))
                                        info.LocalDependencies.Add($"mc_ver:{cleanVer}");
                                }
                            }
                            else if (!string.IsNullOrEmpty(depModId) && depModId != "forge" && depModId != "neoforge")
                            {
                                info.LocalDependencies.Add(depModId);
                            }
                        }
                    }
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Tomlyn Parser Error]: {ex.Message}");
            return null;
        }
    }

    private void IncrementVote(Dictionary<string, int> dict, string key)
    {
        if (dict.ContainsKey(key)) dict[key]++;
        else dict[key] = 1;
    }

    private string CleanVersionString(string raw)
    {
        // Очищает строки вроде ">=1.20.1", "~1.20.1", "" до чистого "1.20.1"
        var chars = raw.Where(c => char.IsDigit(c) || c == '.').ToArray();
        string clean = new string(chars).Trim('.');

        // Берем только мажорную часть патча, если строка склеилась (например "1.20.11.20.2" -> "1.20.1")
        if (clean.Length > 7 && clean.IndexOf('.', 4) > 0)
        {
            clean = clean.Substring(0, clean.IndexOf('.', 4));
        }

        return clean;
    }

    private string GetTomlValue(string toml, string key)
    {
        // Находит строку вида key="value" или key = 'value'
        var line = toml.Split('\n').FirstOrDefault(l => l.Trim().StartsWith(key));
        if (line == null) return string.Empty;
        var parts = line.Split('=');
        if (parts.Length < 2) return string.Empty;
        return parts[1].Trim(' ', '\r', '\t', '"', '\'');
    }
}