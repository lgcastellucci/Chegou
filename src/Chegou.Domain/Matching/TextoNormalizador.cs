using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Chegou.Domain.Matching;

public static class TextoNormalizador
{
    private static readonly HashSet<string> Ignorar = new() { "DA", "DE", "DO", "DAS", "DOS", "E" };

    /// <summary>Maiúsculas, sem acento, sem pontuação e sem preposições. Usado para nomes.</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        var limpo = Regex.Replace(SemAcento(texto).ToUpperInvariant(), @"[^A-Z0-9 ]", " ");
        var partes = limpo.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => !Ignorar.Contains(p));
        return string.Join(' ', partes);
    }

    /// <summary>Para bloco/apartamento: só letras e números, maiúsculo ("Ap. 302-B" -> "AP302B").</summary>
    public static string NormalizarCodigo(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        return Regex.Replace(SemAcento(texto).ToUpperInvariant(), @"[^A-Z0-9]", "");
    }

    private static string SemAcento(string texto)
    {
        var d = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }
}
