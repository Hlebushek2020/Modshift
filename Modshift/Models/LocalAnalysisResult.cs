using System.Collections.Generic;

namespace Modshift.Models;

public class LocalAnalysisResult
{
    public string DetectedVersion { get; set; } = "1.20.1"; // Дефолт, если не определили
    public string DetectedLoader { get; set; } = "Unknown";
    public List<LocalModInfo> Mods { get; set; } = new();
}

public class LocalModInfo
{
    public string FileName { get; set; } = string.Empty;
    public string ModId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ModVersion { get; set; } = string.Empty;
    public string Loader { get; set; } = "Unknown";
    
    // Список локальных ID модов, от которых зависит данный файл
    public List<string> LocalDependencies { get; set; } = new();
}