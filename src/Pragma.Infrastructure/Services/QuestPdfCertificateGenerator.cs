using Pragma.Application.Contracts;
using Pragma.Domain.Enums;
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
            if (model.Type == CertificateType.Attendance)
            {
                RenderAttendanceCertificate(container, model, qrCodeBytes);
            }
            else
            {
                RenderDiplomaCertificate(container, model, qrCodeBytes);
            }
        });

        return document.GeneratePdf();
    }

    private static void RenderDiplomaCertificate(IDocumentContainer container, CertificateRenderModel model, byte[] qrCodeBytes)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(18, Unit.Point);
            page.PageColor(Color.FromHex("#FFFDF7"));

            // İkiqat zərif çərçivə: Dərin sürməyi və qızılı kənarlar
            page.Content().Border(3.5f).BorderColor(Color.FromHex("#0F172A")).Padding(5).Border(2).BorderColor(Color.FromHex("#D97706")).PaddingVertical(20).PaddingHorizontal(28).Column(column =>
            {
                // 1. Üst Akademiya Brendi və Başlıq
                column.Item().AlignCenter().Text("PRAGMA ACADEMY OF ADVANCED TECHNOLOGY")
                    .FontSize(11.5f)
                    .SemiBold()
                    .FontColor(Color.FromHex("#1E293B"))
                    .LetterSpacing(0.24f);

                column.Item().PaddingTop(2).AlignCenter().Text("Beynəlxalq Rəqəmsal Təhsil və Peşəkar İxtisaslaşma Mərkəzi • International Center for IT & Engineering")
                    .FontSize(8.5f)
                    .Italic()
                    .FontColor(Color.FromHex("#64748B"));

                // Bölücü zərif qızılı xətt
                column.Item().PaddingVertical(8).Row(r =>
                {
                    r.RelativeItem().LineHorizontal(1.5f).LineColor(Color.FromHex("#D97706"));
                    r.ConstantItem(60).AlignCenter().Text("•   •   •").FontSize(9).Bold().FontColor(Color.FromHex("#D97706"));
                    r.RelativeItem().LineHorizontal(1.5f).LineColor(Color.FromHex("#D97706"));
                });

                // 2. Böyük Diplom Başlığı
                column.Item().PaddingTop(4).AlignCenter().Text("PEŞƏKAR MƏZUNİYYƏT DİPLOMU")
                    .FontSize(25)
                    .ExtraBold()
                    .FontColor(Color.FromHex("#0F172A"))
                    .LetterSpacing(0.08f);

                column.Item().PaddingTop(2).AlignCenter().Text("DIPLOMA OF PROFESSIONAL MASTERY & ACADEMIC EXCELLENCE")
                    .FontSize(9.5f)
                    .Bold()
                    .FontColor(Color.FromHex("#D97706"))
                    .LetterSpacing(0.2f);

                // 3. Təqdimat və Məzun Adı
                column.Item().PaddingTop(12).AlignCenter().Text("Bu rəsmi diplom fəxrlə təsdiq edir ki,")
                    .FontSize(11.5f)
                    .Italic()
                    .FontColor(Color.FromHex("#64748B"));

                column.Item().PaddingTop(4).AlignCenter().Text(model.StudentFullName)
                    .FontSize(28)
                    .ExtraBold()
                    .FontColor(Color.FromHex("#0F172A"));

                column.Item().PaddingTop(4).AlignCenter().Width(280).LineHorizontal(2).LineColor(Color.FromHex("#D97706"));

                // 4. Kurs Məlumatı
                column.Item().PaddingTop(10).AlignCenter().Text($"aşağıdakı ixtisaslaşdırılmış peşəkar tədris proqramını bütün modul tapşırıqları, laboratoriya işləri və yekun layihə üzrə {model.GradePercentage:0.0}% rəsmi müvəffəqiyyət nəticəsi ilə uğurla başa vurmuş və Məzun diplomuna layiq görülmüşdür:")
                    .FontSize(11)
                    .FontColor(Color.FromHex("#475569"));

                column.Item().PaddingTop(8).AlignCenter().Width(620).Background(Color.FromHex("#F1F5F9")).Border(1.5f).BorderColor(Color.FromHex("#CBD5E1")).PaddingVertical(9).PaddingHorizontal(24).AlignCenter().Text(model.CourseTitle)
                    .FontSize(19)
                    .Bold()
                    .FontColor(Color.FromHex("#1E3A8A"));

                // 3 Ədəd Zəngin Göstərici Kartı (Diplom üçün)
                column.Item().PaddingTop(8).Row(r =>
                {
                    r.RelativeItem().PaddingHorizontal(5).Background(Color.FromHex("#F0FDF4")).Border(1).BorderColor(Color.FromHex("#BBF7D0")).PaddingVertical(5).AlignCenter().Text($"Yekun Nəticə: {model.GradePercentage:0.0}% Müvəffəqiyyət").FontSize(8.5f).SemiBold().FontColor(Color.FromHex("#15803D"));
                    r.RelativeItem().PaddingHorizontal(5).Background(Color.FromHex("#FEFCE8")).Border(1).BorderColor(Color.FromHex("#FEF08A")).PaddingVertical(5).AlignCenter().Text("Akademik Dərəcə: Peşəkar Diplom").FontSize(8.5f).SemiBold().FontColor(Color.FromHex("#A16207"));
                    r.RelativeItem().PaddingHorizontal(5).Background(Color.FromHex("#EFF6FF")).Border(1).BorderColor(Color.FromHex("#BFDBFE")).PaddingVertical(5).AlignCenter().Text("Rəsmi Elektron Reyestr Qeydiyyatı").FontSize(8.5f).SemiBold().FontColor(Color.FromHex("#1D4ED8"));
                });

                column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Color.FromHex("#CBD5E1"));

                // 5. Alt blok: Tarix, QR Kod və Rəsmi İmza
                column.Item().Row(row =>
                {
                    // Sol tərəf: Sənəd Məlumatları
                    row.RelativeItem(1.2f).Column(c =>
                    {
                        c.Item().Text($"Verilmə Tarixi: {model.IssuedAtUtc:dd MMMM yyyy}")
                            .FontSize(10)
                            .SemiBold()
                            .FontColor(Color.FromHex("#0F172A"));

                        c.Item().PaddingTop(3).Text($"Diplom №: {model.CertificateCode}")
                            .FontSize(10)
                            .Bold()
                            .FontColor(Color.FromHex("#B45309"));

                        c.Item().PaddingTop(3).Text("Status: Rəsmi Təsdiqlənmişdir")
                            .FontSize(8.5f)
                            .SemiBold()
                            .FontColor(Color.FromHex("#15803D"));

                        c.Item().PaddingTop(3).Text("Elektron sənəd qanuni hüquqi qüvvəyə malikdir.")
                            .FontSize(7.5f)
                            .Italic()
                            .FontColor(Color.FromHex("#94A3B8"));
                    });

                    // Mərkəz: QR Kod və Doğrulama
                    if (qrCodeBytes != null && qrCodeBytes.Length > 0)
                    {
                        row.ConstantItem(90).Column(c =>
                        {
                            c.Item().AlignCenter().Width(62).Height(62).Image(qrCodeBytes);
                            c.Item().PaddingTop(2).AlignCenter().Text("RƏSMİ ELEKTRON DOĞRULAMA")
                                .FontSize(6.5f)
                                .Bold()
                                .FontColor(Color.FromHex("#B45309"));
                            c.Item().AlignCenter().Text("Skan edərək yoxlayın")
                                .FontSize(6)
                                .FontColor(Color.FromHex("#64748B"));
                        });
                    }

                    // Sağ tərəf: Təlimçi və Rəsmi Təsdiq İmza Bloku
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("Tədris Rəhbəri / Baş Müəllim:")
                            .FontSize(8.5f)
                            .FontColor(Color.FromHex("#64748B"));

                        c.Item().PaddingTop(2).AlignRight().Text(model.InstructorName)
                            .FontSize(12.5f)
                            .Bold()
                            .FontColor(Color.FromHex("#0F172A"));

                        c.Item().PaddingTop(1).AlignRight().Text("Pragma Academy Akademik Şura Sədri")
                            .FontSize(7.5f)
                            .Italic()
                            .FontColor(Color.FromHex("#64748B"));

                        c.Item().PaddingTop(5).AlignRight().Width(135).LineHorizontal(1.5f).LineColor(Color.FromHex("#64748B"));

                        c.Item().PaddingTop(3).AlignRight().Text("[ RƏQƏMSAL DİPLOM İMZASI ]")
                            .FontSize(6.5f)
                            .Bold()
                            .FontColor(Color.FromHex("#0284C7"));
                    });
                });
            });
        });
    }

    private static void RenderAttendanceCertificate(IDocumentContainer container, CertificateRenderModel model, byte[] qrCodeBytes)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(18, Unit.Point);
            page.PageColor(Color.FromHex("#F8FAFC"));

            // İkiqat zərif çərçivə: Dərin sürməyi və zümrüd yaşılı kənarlar
            page.Content().Border(3.5f).BorderColor(Color.FromHex("#0F172A")).Padding(5).Border(2).BorderColor(Color.FromHex("#059669")).PaddingVertical(20).PaddingHorizontal(28).Column(column =>
            {
                // 1. Üst Akademiya Brendi və Başlıq
                column.Item().AlignCenter().Text("PRAGMA ACADEMY OF ADVANCED TECHNOLOGY")
                    .FontSize(11.5f)
                    .SemiBold()
                    .FontColor(Color.FromHex("#1E293B"))
                    .LetterSpacing(0.24f);

                column.Item().PaddingTop(2).AlignCenter().Text("Beynəlxalq Rəqəmsal Təhsil və Peşəkar İxtisaslaşma Mərkəzi • International Center for IT & Engineering")
                    .FontSize(8.5f)
                    .Italic()
                    .FontColor(Color.FromHex("#64748B"));

                // Bölücü zərif zümrüd xətt
                column.Item().PaddingVertical(8).Row(r =>
                {
                    r.RelativeItem().LineHorizontal(1.5f).LineColor(Color.FromHex("#059669"));
                    r.ConstantItem(60).AlignCenter().Text("•   •   •").FontSize(9).Bold().FontColor(Color.FromHex("#059669"));
                    r.RelativeItem().LineHorizontal(1.5f).LineColor(Color.FromHex("#059669"));
                });

                // 2. Böyük İştirak Sertifikatı Başlığı
                column.Item().PaddingTop(4).AlignCenter().Text("DƏRSDƏ İŞTİRAK SERTİFİKATI")
                    .FontSize(25)
                    .ExtraBold()
                    .FontColor(Color.FromHex("#0F172A"))
                    .LetterSpacing(0.08f);

                column.Item().PaddingTop(2).AlignCenter().Text("CERTIFICATE OF ATTENDANCE & COURSE COMPLETION")
                    .FontSize(9.5f)
                    .Bold()
                    .FontColor(Color.FromHex("#059669"))
                    .LetterSpacing(0.2f);

                // 3. Təqdimat və İştirakçı Adı
                column.Item().PaddingTop(12).AlignCenter().Text("Bu rəsmi sənəd təsdiq edir ki,")
                    .FontSize(11.5f)
                    .Italic()
                    .FontColor(Color.FromHex("#64748B"));

                column.Item().PaddingTop(4).AlignCenter().Text(model.StudentFullName)
                    .FontSize(28)
                    .ExtraBold()
                    .FontColor(Color.FromHex("#0F172A"));

                column.Item().PaddingTop(4).AlignCenter().Width(280).LineHorizontal(2).LineColor(Color.FromHex("#059669"));

                // 4. Kurs Məlumatı
                column.Item().PaddingTop(10).AlignCenter().Text("aşağıdakı ixtisaslaşdırılmış tədris proqramı üzrə təşkil olunmuş bütün dərslərdə dinləyici qismində müntəzəm iştirak etmiş və tədris kursunu başa vurmuşdur:")
                    .FontSize(11)
                    .FontColor(Color.FromHex("#475569"));

                column.Item().PaddingTop(8).AlignCenter().Width(620).Background(Color.FromHex("#F0FDF4")).Border(1.5f).BorderColor(Color.FromHex("#A7F3D0")).PaddingVertical(9).PaddingHorizontal(24).AlignCenter().Text(model.CourseTitle)
                    .FontSize(19)
                    .Bold()
                    .FontColor(Color.FromHex("#065F46"));

                // 3 Ədəd Zəngin Göstərici Kartı (İştirak üçün)
                column.Item().PaddingTop(8).Row(r =>
                {
                    r.RelativeItem().PaddingHorizontal(5).Background(Color.FromHex("#F0FDF4")).Border(1).BorderColor(Color.FromHex("#BBF7D0")).PaddingVertical(5).AlignCenter().Text("Tədris Formatı: Dinləyici İştirakı").FontSize(8.5f).SemiBold().FontColor(Color.FromHex("#047857"));
                    r.RelativeItem().PaddingHorizontal(5).Background(Color.FromHex("#F0FDFA")).Border(1).BorderColor(Color.FromHex("#99F6E4")).PaddingVertical(5).AlignCenter().Text("Davamiyyət Norması: Ödənilib").FontSize(8.5f).SemiBold().FontColor(Color.FromHex("#0D9488"));
                    r.RelativeItem().PaddingHorizontal(5).Background(Color.FromHex("#EFF6FF")).Border(1).BorderColor(Color.FromHex("#BFDBFE")).PaddingVertical(5).AlignCenter().Text("Rəsmi Elektron Reyestr Qeydiyyatı").FontSize(8.5f).SemiBold().FontColor(Color.FromHex("#1D4ED8"));
                });

                column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Color.FromHex("#CBD5E1"));

                // 5. Alt blok: Tarix, QR Kod və Rəsmi İmza
                column.Item().Row(row =>
                {
                    // Sol tərəf: Sənəd Məlumatları
                    row.RelativeItem(1.2f).Column(c =>
                    {
                        c.Item().Text($"Verilmə Tarixi: {model.IssuedAtUtc:dd MMMM yyyy}")
                            .FontSize(10)
                            .SemiBold()
                            .FontColor(Color.FromHex("#0F172A"));

                        c.Item().PaddingTop(3).Text($"Sertifikat №: {model.CertificateCode}")
                            .FontSize(10)
                            .Bold()
                            .FontColor(Color.FromHex("#059669"));

                        c.Item().PaddingTop(3).Text("Status: İştirak Təsdiqlənmişdir")
                            .FontSize(8.5f)
                            .SemiBold()
                            .FontColor(Color.FromHex("#047857"));

                        c.Item().PaddingTop(3).Text("Elektron sənəd qanuni hüquqi qüvvəyə malikdir.")
                            .FontSize(7.5f)
                            .Italic()
                            .FontColor(Color.FromHex("#94A3B8"));
                    });

                    // Mərkəz: QR Kod və Doğrulama
                    if (qrCodeBytes != null && qrCodeBytes.Length > 0)
                    {
                        row.ConstantItem(90).Column(c =>
                        {
                            c.Item().AlignCenter().Width(62).Height(62).Image(qrCodeBytes);
                            c.Item().PaddingTop(2).AlignCenter().Text("RƏSMİ ELEKTRON DOĞRULAMA")
                                .FontSize(6.5f)
                                .Bold()
                                .FontColor(Color.FromHex("#059669"));
                            c.Item().AlignCenter().Text("Skan edərək yoxlayın")
                                .FontSize(6)
                                .FontColor(Color.FromHex("#64748B"));
                        });
                    }

                    // Sağ tərəf: Təlimçi və Rəsmi Təsdiq İmza Bloku
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("Tədris Rəhbəri / Baş Müəllim:")
                            .FontSize(8.5f)
                            .FontColor(Color.FromHex("#64748B"));

                        c.Item().PaddingTop(2).AlignRight().Text(model.InstructorName)
                            .FontSize(12.5f)
                            .Bold()
                            .FontColor(Color.FromHex("#0F172A"));

                        c.Item().PaddingTop(1).AlignRight().Text("Pragma Academy Akademik Şura Sədri")
                            .FontSize(7.5f)
                            .Italic()
                            .FontColor(Color.FromHex("#64748B"));

                        c.Item().PaddingTop(5).AlignRight().Width(135).LineHorizontal(1.5f).LineColor(Color.FromHex("#64748B"));

                        c.Item().PaddingTop(3).AlignRight().Text("[ ELEKTRON İŞTİRAK TƏSDİQİ ]")
                            .FontSize(6.5f)
                            .Bold()
                            .FontColor(Color.FromHex("#0D9488"));
                    });
                });
            });
        });
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
