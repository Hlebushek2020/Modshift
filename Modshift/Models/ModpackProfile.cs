using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Modshift.Models;

public class ModpackProfile
{
    public Version SchemaVersion { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    public string Version { get; set; }
    public string Loader { get; set; }
    public string ModsFolderPath { get; set; }
    public string Description { get; set; }

    // ИСПРАВЛЕНО: Теперь профиль на диске хранит историю и слепок всех модов с их API-статусами!
    public List<SavedModMetadata> CachedMods { get; set; } = new();
}

/// <summary>
/// Структура сохранения данных конкретного мода на диске
/// </summary>
public class SavedModMetadata
{
    public string FileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string ModId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ModVersion { get; set; } = string.Empty;
    public string Loader { get; set; } = string.Empty;
    public string ApiStatusComment { get; set; } = string.Empty; // Сюда запишется "🟢 Доступно: v..." или ошибка
    public string? DownloadUrl { get; set; } // Прямая ссылка на скачивание .jar с Modrinth
}