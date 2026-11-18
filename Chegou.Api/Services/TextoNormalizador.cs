using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Chegou.Api.Services
{
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

        /// <summary>Para bloco/apartamento: só letras e números, maiúsculo ("302-b" -> "302B").</summary>
        public static string NormalizarCodigo(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            return Regex.Replace(SemAcento(texto).ToUpperInvariant(), @"[^A-Z0-9]", "");
        }

        public static string NormalizarNumero(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            return Regex.Replace(SemAcento(texto).ToUpperInvariant(), @"[^0-9]", "");
        }

        /// <summary>
        /// Para o código do condomínio (header X-Condominio-Id): maiúsculas, sem acento e só letras, números e hífen
        /// ("dev-chegou-0001" -> "DEV-CHEGOU-0001"). Ao contrário de Normalizar, não remove palavras como DA/DE/DO.
        /// Qualquer outro caractere (aspas, espaço, ponto e vírgula...) é descartado.
        /// </summary>
        public static string NormalizarCondominioCodigo(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            return Regex.Replace(SemAcento(texto).ToUpperInvariant(), @"[^A-Z0-9\-]", "");
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
}
