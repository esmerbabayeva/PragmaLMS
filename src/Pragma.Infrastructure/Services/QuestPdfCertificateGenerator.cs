using Pragma.Application.Contracts;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pragma.Infrastructure.Services;

public class QuestPdfCertificateGenerator : ICertificateGenerator
{
    static QuestPdfCertificateGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GeneratePdf(CertificateRenderModel model)
    {
        var qrCodeBytes = GenerateQrCodeBytes(model.VerificationUrl);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(25, Unit.Point);
                page.PageColor(Colors.White);

                page.Content().Border(3).BorderColor(Color.FromHex("#D97706")).Padding(8).Border(1.5f).BorderColor(Color.FromHex("#1E293B")).Padding(20).Column(column =>
                {
                    // 1. Başlıq və Brend
                    column.Item().AlignCenter().Text("PRAGMA LEARNING MANAGEMENT SYSTEM")
                        .FontSize(13)
                        .SemiBold()
                        .FontColor(Color.FromHex("#475569"))
                        .LetterSpacing(0.15f);

                    column.Item().PaddingTop(6).AlignCenter().Text("MƏZUNİYYƏT VƏ BİTİRMƏ SERTİFİKATI")
                        .FontSize(24)
                        .Bold()
                        .FontColor(Color.FromHex("#1E293B"));

                    column.Item().PaddingTop(2).AlignCenter().Text("Certificate of Completion")
                        .FontSize(11)
                        .Italic()
                        .FontColor(Color.FromHex("#94A3B8"));

                    column.Item().PaddingVertical(10).AlignCenter().LineHorizontal(1).LineColor(Color.FromHex("#E2E8F0"));

                    // 2. Mətn və Tələbə Adı
                    column.Item().AlignCenter().Text("Bu rəsmi sənəd təsdiq edir ki,")
                        .FontSize(12)
                        .FontColor(Color.FromHex("#475569"));

                    column.Item().PaddingVertical(8).AlignCenter().Text(model.StudentFullName)
                        .FontSize(26)
                        .Bold()
                        .FontColor(Color.FromHex("#0F172A"));

                    column.Item().AlignCenter().Text("aşağıdakı peşəkar tədris proqramını bütün modul və imtahanları ilə 100% uğurla başa vurmuşdur:")
                        .FontSize(12)
                        .FontColor(Color.FromHex("#475569"));

                    column.Item().PaddingVertical(8).AlignCenter().Text(model.CourseTitle)
                        .FontSize(20)
                        .SemiBold()
                        .FontColor(Color.FromHex("#D97706"));

                    column.Item().PaddingVertical(10).AlignCenter().LineHorizontal(1).LineColor(Color.FromHex("#E2E8F0"));

                    // 3. Alt blok: Tarix, QR Kod və İmza
                    column.Item().Row(row =>
                    {
                        // Sol tərəf: Tarix və Kod
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Verilmə Tarixi: {model.IssuedAtUtc:dd.MM.yyyy}")
                                .FontSize(10)
                                .SemiBold()
                                .FontColor(Color.FromHex("#334155"));

                            c.Item().PaddingTop(2).Text($"Sertifikat №: {model.CertificateCode}")
                                .FontSize(10)
                                .FontColor(Color.FromHex("#64748B"));

                            c.Item().PaddingTop(4).Text("Elektron sənəd qanuni qüvvəyə malikdir.")
                                .FontSize(8)
                                .Italic()
                                .FontColor(Color.FromHex("#94A3B8"));
                        });

                        // Mərkəz: QR Kod
                        if (qrCodeBytes != null && qrCodeBytes.Length > 0)
                        {
                            row.ConstantItem(75).Column(c =>
                            {
                                c.Item().AlignCenter().Width(60).Height(60).Image(qrCodeBytes);
                                c.Item().AlignCenter().Text("Onlayn Doğrulama")
                                    .FontSize(7)
                                    .FontColor(Color.FromHex("#64748B"));
                            });
                        }

                        // Sağ tərəf: Təlimçi / Rəhbər İmza Bloku
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text(model.InstructorName)
                                .FontSize(11)
                                .Bold()
                                .FontColor(Color.FromHex("#1E293B"));

                            c.Item().PaddingTop(2).AlignRight().Text("Tədris Rəhbəri / Müəllim")
                                .FontSize(9)
                                .FontColor(Color.FromHex("#64748B"));

                            c.Item().PaddingTop(8).AlignRight().Width(120).LineHorizontal(1).LineColor(Color.FromHex("#CBD5E1"));
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static byte[] GenerateQrCodeBytes(string url)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            var pngByteQr = new PngByteQRCode(data);
            return pngByteQr.GetGraphic(15);
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }
}
