using System.Security.Cryptography;

namespace FamVault.Storage.Services;

internal static class FileHashService
{
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        using SHA256 sha256 = SHA256.Create();

        using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous);

        byte[] buffer = new byte[81920];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(
                   buffer,
                   0,
                   buffer.Length,
                   cancellationToken)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sha256.TransformBlock(buffer, 0, bytesRead, buffer, 0);
        }

        sha256.TransformFinalBlock([], 0, 0);
        return BitConverter.ToString(sha256.Hash).Replace("-", "").ToLowerInvariant();
    }
}
