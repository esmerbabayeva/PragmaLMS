using Pragma.Application.Common;

namespace Pragma.Application.Contracts;

public interface IFileStorage
{
    Task<Result<string>> UploadFileAsync(
        Stream contentStream,
        string originalFileName,
        string contentType,
        long maxSizeBytes,
        string[] allowedMimeTypes,
        string subFolder,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default);

    Task<(byte[] Bytes, string ContentType)?> ReadFileAsync(string relativePath, CancellationToken cancellationToken = default);
}
