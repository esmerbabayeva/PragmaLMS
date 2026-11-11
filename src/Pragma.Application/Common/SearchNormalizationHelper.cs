using System.Text;

namespace Pragma.Application.Common;

public static class SearchNormalizationHelper
{
    public static string ToAscii(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var sb = new StringBuilder();
        foreach (var ch in text.ToLowerInvariant())
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

    public static string ToAzeri(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var sb = new StringBuilder();
        foreach (var ch in text.ToLowerInvariant())
        {
            sb.Append(ch switch
            {
                'u' => 'ü',
                'o' => 'ö',
                's' => 'ş',
                'c' => 'ç',
                'g' => 'ğ',
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

        var az = ToAzeri(raw);
        if (!string.IsNullOrWhiteSpace(az)) list.Add(az);

        return [.. list];
    }
}
