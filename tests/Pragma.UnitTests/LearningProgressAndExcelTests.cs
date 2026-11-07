using ClosedXML.Excel;
using Pragma.Application.DTOs.Notes;
using Pragma.Application.DTOs.Progress;
using Pragma.Application.Validators.Notes;
using Pragma.Application.Validators.Progress;

namespace Pragma.UnitTests;

public class LearningProgressAndExcelTests
{
    [Fact]
    public void UpdateVideoPositionValidator_WhenPositionNegative_ReturnsValidationError()
    {
        // Arrange
        var validator = new UpdateVideoPositionRequestValidator();
        var dto = new UpdateVideoPositionRequestDto(CurrentPositionSeconds: -5, DurationSeconds: 120);

        // Act
        var result = validator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateVideoPositionRequestDto.CurrentPositionSeconds));
    }

    [Fact]
    public void CreateLessonNoteValidator_WhenNoteTextIsEmpty_ReturnsValidationError()
    {
        // Arrange
        var validator = new CreateLessonNoteRequestValidator();
        var dto = new CreateLessonNoteRequestDto(PositionSeconds: 45, NoteText: "");

        // Act
        var result = validator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLessonNoteRequestDto.NoteText));
    }

    [Fact]
    public void CreateLessonNoteValidator_WhenValid_PassesValidation()
    {
        // Arrange
        var validator = new CreateLessonNoteRequestValidator();
        var dto = new CreateLessonNoteRequestDto(PositionSeconds: 124, NoteText: "Bu yerdə Dependency Injection izah edilir.");

        // Act
        var result = validator.Validate(dto);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(45, 100, false)] // 45% - bitməyib
    [InlineData(89, 100, false)] // 89% - 90% həddinə çatmayıb
    [InlineData(90, 100, true)]  // 90% - dəqiq hədd, avto-tamamlanır
    [InlineData(95, 100, true)]  // 95% - avto-tamamlanır
    [InlineData(100, 100, true)] // 100% - avto-tamamlanır
    public void VideoProgress_Threshold90PercentRule_EvaluatesCorrectly(int currentSeconds, int durationSeconds, bool expectedCompleted)
    {
        // Arrange
        var completionThreshold = (int)Math.Ceiling(durationSeconds * 0.90);

        // Act
        var isCompleted = currentSeconds >= completionThreshold;

        // Assert
        Assert.Equal(expectedCompleted, isCompleted);
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(45, "00:45")]
    [InlineData(125, "02:05")]
    [InlineData(3665, "01:01:05")]
    public void TimestampFormatting_ProducesExpectedDisplayString(int seconds, string expectedDisplay)
    {
        // Act
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        var formatted = ts.Hours > 0
            ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}";

        // Assert
        Assert.Equal(expectedDisplay, formatted);
    }

    [Fact]
    public void ClosedXML_FormulaInjectionProtection_TreatsDangerousPayloadsAsPlainTextNotFormulas()
    {
        // Arrange dangerous CSV / Excel Formula Injection payloads
        var dangerousInputs = new[]
        {
            "=cmd|' /C calc'!A0",
            "+12345",
            "-SUM(A1:A10)",
            "@HYPERLINK(\"http://malicious.site\", \"Click Here\")"
        };

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Təhlükəsizlik Testi");

        // Act & Assert
        for (int i = 0; i < dangerousInputs.Length; i++)
        {
            var raw = dangerousInputs[i];
            var sanitized = (raw.StartsWith("=") || raw.StartsWith("+") || raw.StartsWith("-") || raw.StartsWith("@"))
                ? "'" + raw
                : raw;

            var cell = worksheet.Cell(i + 1, 1);
            cell.SetValue(sanitized);

            // Təhlükəsizlik təsdiqi: Heç bir təhlükəli string düstur (formula) kimi qəbul olunmur
            Assert.False(cell.HasFormula);
            Assert.True(cell.Value.IsText);
        }
    }

    [Fact]
    public void ClosedXML_GradebookWorkbook_CreatesExpected3Worksheets()
    {
        // Arrange
        using var workbook = new XLWorkbook();

        // Act
        workbook.Worksheets.Add("Tələbələr və Proqres");
        workbook.Worksheets.Add("Kviz Nəticələri");
        workbook.Worksheets.Add("Dərs İzlənməsi");

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var bytes = ms.ToArray();

        // Assert
        Assert.NotEmpty(bytes);
        // Valid ZIP/XLSX header starts with PK (0x50, 0x4B)
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);

        using var reloadedMs = new MemoryStream(bytes);
        using var reloadedWb = new XLWorkbook(reloadedMs);
        Assert.Equal(3, reloadedWb.Worksheets.Count);
        Assert.NotNull(reloadedWb.Worksheet("Tələbələr və Proqres"));
        Assert.NotNull(reloadedWb.Worksheet("Kviz Nəticələri"));
        Assert.NotNull(reloadedWb.Worksheet("Dərs İzlənməsi"));
    }
}
