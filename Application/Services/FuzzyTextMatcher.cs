using System.Text.RegularExpressions;

namespace ConceptJewelleryPro.Application.Services;

public readonly struct FuzzyMatchResult
{
    public bool IsMatch { get; }
    public double Score { get; }
    public string MatchedKeyword { get; }
    public string Reason { get; }

    public FuzzyMatchResult(bool isMatch, double score, string matchedKeyword, string reason)
    {
        IsMatch = isMatch;
        Score = score;
        MatchedKeyword = matchedKeyword;
        Reason = reason;
    }

    public static FuzzyMatchResult NoMatch => new(false, 0, string.Empty, "No match");
}

public static class FuzzyTextMatcher
{
    private static readonly HashSet<string> BankingStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "NEFT", "RTGS", "IMPS", "UPI", "DR", "CR", "TRF", "TRANSFER", "PAYMENT", "PAYMENTTO",
        "INB", "CMS", "INF", "BIL", "BY", "TO", "FROM", "A/C", "AC", "ACCOUNT", "BRANCH",
        "PVT", "LTD", "LIMITED", "PRIVATE", "CORP", "CORPORATION", "CO", "AND", "THE", "FOR",
        "TXN", "POS", "BANK", "DEP", "WDL", "ATM", "CLEARING", "CHQ", "CHEQUE", "REF", "NO"
    };

    public static FuzzyMatchResult MatchParty(string bankNarration, string erpPartyOrParticulars)
    {
        if (string.IsNullOrWhiteSpace(bankNarration) || string.IsNullOrWhiteSpace(erpPartyOrParticulars))
            return FuzzyMatchResult.NoMatch;

        var cleanBank = CleanText(bankNarration);
        var cleanErp = CleanText(erpPartyOrParticulars);

        if (cleanBank.Length == 0 || cleanErp.Length == 0)
            return FuzzyMatchResult.NoMatch;

        // 1. Direct Substring Match
        if (cleanErp.Length >= 4 && cleanBank.Contains(cleanErp, StringComparison.OrdinalIgnoreCase))
        {
            return new FuzzyMatchResult(true, 1.0, erpPartyOrParticulars.Trim(), $"Exact party name found in narration: '{erpPartyOrParticulars.Trim()}'");
        }

        var bankTokens = Tokenize(bankNarration);
        var erpTokens = Tokenize(erpPartyOrParticulars);

        if (erpTokens.Count == 0 || bankTokens.Count == 0)
            return FuzzyMatchResult.NoMatch;

        // 2. Token Overlap & Fuzzy Token Matching
        int matchedTokens = 0;
        string bestMatchedKeyword = string.Empty;
        double totalTokenScore = 0;

        foreach (var erpToken in erpTokens)
        {
            if (bankTokens.Contains(erpToken))
            {
                matchedTokens++;
                totalTokenScore += 1.0;
                if (string.IsNullOrEmpty(bestMatchedKeyword)) bestMatchedKeyword = erpToken;
                continue;
            }

            bool prefixOrSubstringFound = false;
            foreach (var bToken in bankTokens)
            {
                if (bToken.Length >= 4 && erpToken.Length >= 4)
                {
                    if (bToken.Contains(erpToken, StringComparison.OrdinalIgnoreCase) ||
                        erpToken.Contains(bToken, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedTokens++;
                        totalTokenScore += 0.85;
                        if (string.IsNullOrEmpty(bestMatchedKeyword)) bestMatchedKeyword = erpToken;
                        prefixOrSubstringFound = true;
                        break;
                    }

                    double sim = CalculateSimilarity(bToken, erpToken);
                    if (sim >= 0.80)
                    {
                        matchedTokens++;
                        totalTokenScore += sim * 0.8;
                        if (string.IsNullOrEmpty(bestMatchedKeyword)) bestMatchedKeyword = $"{erpToken} ~ {bToken}";
                        prefixOrSubstringFound = true;
                        break;
                    }
                }
            }

            if (prefixOrSubstringFound) continue;
        }

        if (matchedTokens == 0)
            return FuzzyMatchResult.NoMatch;

        double coverage = totalTokenScore / erpTokens.Count;
        double confidence = Math.Min(1.0, Math.Round(coverage, 2));

        if (confidence >= 0.40)
        {
            return new FuzzyMatchResult(
                true,
                confidence,
                bestMatchedKeyword,
                $"Party match ({confidence * 100:0}% similarity on keyword '{bestMatchedKeyword}')");
        }

        return FuzzyMatchResult.NoMatch;
    }

    private static string CleanText(string input)
    {
        return Regex.Replace(input, @"[^A-Za-z0-9\s]", " ").Trim();
    }

    private static HashSet<string> Tokenize(string input)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parts = Regex.Split(input, @"[^A-Za-z0-9]+");

        foreach (var p in parts)
        {
            var cleaned = p.Trim();
            if (cleaned.Length >= 3 && !BankingStopWords.Contains(cleaned) && !IsNumeric(cleaned))
            {
                tokens.Add(cleaned);
            }
        }

        return tokens;
    }

    private static bool IsNumeric(string s)
    {
        return s.All(char.IsDigit);
    }

    public static double CalculateSimilarity(string s1, string s2)
    {
        if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        int maxLen = Math.Max(s1.Length, s2.Length);
        if (maxLen == 0) return 1.0;

        int dist = LevenshteinDistance(s1.ToUpperInvariant(), s2.ToUpperInvariant());
        return 1.0 - ((double)dist / maxLen);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
