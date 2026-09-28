using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Modshift.Services;

public class ModHashCalculator
{
    /// <summary>
    /// Асинхронно вычисляет хэш SHA-512 для указанного файла .jar
    /// </summary>
    public async Task<string> CalculateSha512Async(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            // Открываем файл в асинхронном режиме с оптимальным размером буфера (4КБ)
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                useAsync: true);
            using var sha512 = SHA512.Create();

            byte[] hashBytes = await sha512.ComputeHashAsync(stream, cancellationToken);

            // Превращаем байты в плоскую строку hex (например: a1b2c3...)
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Hash Error] Не удалось вычислить хэш для {Path.GetFileName(filePath)}: {ex.Message}");
            return null;
        }
    }
}