using Pragma.Application.Common;

namespace Pragma.Application.Contracts;

public record ExcelFileResult(
    byte[] Content,
    string ContentType,
    string FileName
);

public interface IExcelExporter
{
    Task<Result<ExcelFileResult>> ExportCourseGradebookAsync(Guid courseId, CancellationToken ct = default);
}
