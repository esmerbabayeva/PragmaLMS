using System.Text;

namespace Pragma.Application.Common;

public static class SearchNormalizationHelper
{
    public static string ToAscii(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = text.Replace('İ', 'i').Replace('I', 'i');
        var sb = new StringBuilder();
        foreach (var ch in normalized.ToLowerInvariant())
        {
            sb.Append(ch switch
            {
                'ə' => 'e',
                'ı' => 'i',
                'ö' => 'o',
                'ü' => 'u',
                'ş' => 's',
                'ç' => 'c',
                'ğ' => 'g',
                _ => ch
            });
        }
        return sb.ToString();
    }

    public static List<string> GetSearchTerms(string search)
    {
        var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(search)) return [.. list];

        var raw = search.Trim().ToLowerInvariant();
        list.Add(raw);

        var ascii = ToAscii(raw);
        if (!string.IsNullOrWhiteSpace(ascii)) list.Add(ascii);

        // Əgər sorğuda 'u' varsa 'ü' variantı
        if (raw.Contains('u')) list.Add(raw.Replace('u', 'ü'));
        // Əgər sorğuda 'o' varsa 'ö' variantı
        if (raw.Contains('o')) list.Add(raw.Replace('o', 'ö'));
        // Əgər sorğuda 'e' varsa 'ə' variantı
        if (raw.Contains('e')) list.Add(raw.Replace('e', 'ə'));

        return [.. list];
    }
}
