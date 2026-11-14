using Microsoft.AspNetCore.Hosting;
using Pragma.Application.Common;
using Pragma.Application.Contracts;

namespace Pragma.Infrastructure.Services;

public class LocalFileStorage : IFileStorage
{
    private readonly string _storageRoot;

    public LocalFileStorage(IWebHostEnvironment environment)
    {
        _storageRoot = Path.Combine(environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"), "uploads");
        if (!Directory.Exists(_storageRoot))
        {
            Directory.CreateDirectory(_storageRoot);
        }
    }

    public async Task<Result<string>> UploadFileAsync(
        Stream contentStream,
        string originalFileName,
        string contentType,
        long maxSizeBytes,
        string[] allowedMimeTypes,
        string subFolder,
        CancellationToken cancellationToken = default)
    {
        if (contentStream.Length > maxSizeBytes)
        {
            return Result<string>.Failure(Error.Validation("File.TooLarge", $"Faylın həcmi limiti aşır (maksimum {maxSizeBytes / (1024 * 1024)} MB)."));
        }

        if (allowedMimeTypes.Length > 0 && !allowedMimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return Result<string>.Failure(Error.Validation("File.InvalidType", $"Fayl formatına icazə verilmir: {contentType}"));
        }

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || extension.Contains(".."))
        {
            return Result<string>.Failure(Error.Validation("File.InvalidExtension", "Yanlış fayl uzantısı."));
        }

        var safeSubFolder = Path.GetFileName(subFolder);
        var targetDir = Path.Combine(_storageRoot, safeSubFolder);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var randomName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(targetDir, randomName);

        var canonicalPath = Path.GetFullPath(fullPath);
        if (!canonicalPath.StartsWith(Path.GetFullPath(_storageRoot), StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.Failure(Error.Forbidden("Təhlükəsizlik xətası: Path traversal cəhdi aşkarlandı."));
        }

        await using var fileStream = new FileStream(canonicalPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await contentStream.CopyToAsync(fileStream, cancellationToken);

        var relativeUrl = $"/uploads/{safeSubFolder}/{randomName}".Replace('\\', '/');
        return Result<string>.Success(relativeUrl);
    }

    public Task<bool> DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var cleanRelative = relativePath.TrimStart('/', '\\').Replace("uploads/", string.Empty, StringComparison.OrdinalIgnoreCase);
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, cleanRelative));

        if (!fullPath.StartsWith(Path.GetFullPath(_storageRoot), StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public async Task<(byte[] Bytes, string ContentType)?> ReadFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var cleanRelative = relativePath.TrimStart('/', '\\').Replace("uploads/", string.Empty, StringComparison.OrdinalIgnoreCase);
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, cleanRelative));

        if (!fullPath.StartsWith(Path.GetFullPath(_storageRoot), StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        var contentType = "application/octet-stream";
        return (bytes, contentType);
    }
}
