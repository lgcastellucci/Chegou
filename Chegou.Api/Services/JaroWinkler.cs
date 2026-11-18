
namespace Chegou.Api.Services
{
    public static class JaroWinkler
    {
        public static double Calcular(string a, string b)
        {
            if (a.Length == 0 || b.Length == 0) return 0;
            if (a == b) return 1;

            int range = Math.Max(Math.Max(a.Length, b.Length) / 2 - 1, 0);
            var aMatch = new bool[a.Length];
            var bMatch = new bool[b.Length];
            int matches = 0;

            for (int i = 0; i < a.Length; i++)
            {
                int start = Math.Max(0, i - range);
                int end = Math.Min(i + range + 1, b.Length);
                for (int j = start; j < end; j++)
                {
                    if (bMatch[j] || a[i] != b[j]) continue;
                    aMatch[i] = bMatch[j] = true;
                    matches++;
                    break;
                }
            }
            if (matches == 0) return 0;

            int transpositions = 0, k = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (!aMatch[i]) continue;
                while (!bMatch[k]) k++;
                if (a[i] != b[k]) transpositions++;
                k++;
            }

            double m = matches;
            double jaro = (m / a.Length + m / b.Length + (m - transpositions / 2.0) / m) / 3.0;

            int prefix = 0;
            for (int i = 0; i < Math.Min(4, Math.Min(a.Length, b.Length)); i++)
            {
                if (a[i] == b[i]) prefix++; else break;
            }
            return jaro + prefix * 0.1 * (1 - jaro);
        }
    }
}
