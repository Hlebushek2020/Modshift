using System.Text.Json.Serialization;
using Modshift.Models;

namespace Modshift;

[JsonSourceGenerationOptions(
    WriteIndented = true,                                      // Красивый отступ с переносами строк
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, // Автоматический перевод CamelCase в snake_case
    PropertyNameCaseInsensitive = true                        // Чтение ключей без учета регистра (защита от опечаток)
)]
[JsonSerializable(typeof(ModpackProfile))]
internal partial class AppConfigJsonContext : JsonSerializerContext
{
}