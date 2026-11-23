using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ConceptJewelleryPro.Application.Abstractions;
using ConceptJewelleryPro.Domain.Entities;
using ExcelDataReader;

namespace ConceptJewelleryPro.WPF.Services;

public sealed class BankStatementParserService : IBankStatementParserService
{
    static BankStatementParserService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        AppDomain.CurrentDomain.AssemblyResolve += (s, args) =>
        {
            try
            {
                var requestedName = new System.Reflection.AssemblyName(args.Name).Name;
                if (string.IsNullOrWhiteSpace(requestedName))
                    return null;

                var candidateFolders = new[]
                {
                    AppContext.BaseDirectory,
                    Path.GetDirectoryName(typeof(BankStatementParserService).Assembly.Location),
                    AppDomain.CurrentDomain.BaseDirectory
                };

                foreach (var folder in candidateFolders)
                {
                    if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                        continue;

                    var targetPath = Path.Combine(folder, requestedName + ".dll");
                    if (File.Exists(targetPath))
                    {
                        return System.Reflection.Assembly.LoadFrom(targetPath);
                    }
                }
            }
            catch { }
            return null;
        };
    }

    private static readonly string[] KnownBanks =
    [
        "HDFC BANK", "HDFC", "ICICI BANK", "ICICI", "STATE BANK OF INDIA", "SBI",
        "AXIS BANK", "AXIS", "KOTAK MAHINDRA", "KOTAK", "CANARA BANK", "CANARA",
        "PUNJAB NATIONAL BANK", "PNB", "BANK OF BARODA", "BOB", "UNION BANK", "INDUSIND BANK"
    ];

    public async Task<BankStatementParseResult> ParseStatementAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("Bank statement file not found.", filePath);

        // Ensure encoding provider is active for legacy .xls files
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is not ".xlsx" and not ".xls" and not ".csv")
        {
            return new BankStatementParseResult
            {
                BankName = DetectBankNameFromText(Path.GetFileName(filePath))
            };
        }

        return await Task.Run(() =>
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ext == ".csv"
                ? ExcelReaderFactory.CreateCsvReader(stream)
                : ExcelReaderFactory.CreateReader(stream);

            var dataSet = reader.AsDataSet();
            if (dataSet.Tables.Count == 0)
                return new BankStatementParseResult();

            // Pick table with most non-empty rows
            DataTable targetTable = dataSet.Tables[0];
            foreach (DataTable t in dataSet.Tables)
            {
                if (t.Rows.Count > targetTable.Rows.Count)
                    targetTable = t;
            }

            return ParseDataTable(targetTable, Path.GetFileName(filePath));
        }, cancellationToken);
    }

    private static BankStatementParseResult ParseDataTable(DataTable table, string fileName)
    {
        var result = new BankStatementParseResult
        {
            BankName = DetectBankNameFromText(fileName)
        };

        if (table.Rows.Count == 0 || table.Columns.Count == 0)
            return result;

        int headerRowIndex = -1;
        int colDate = -1;
        int colValueDate = -1;
        int colDesc = -1;
        int colRef = -1;
        int colDebit = -1;
        int colCredit = -1;
        int colBalance = -1;

        // 1. Scan early rows to identify Bank Name, Account Number and Header Row
        int scanRows = Math.Min(100, table.Rows.Count);
        for (int r = 0; r < scanRows; r++)
        {
            var row = table.Rows[r];
            var rowText = string.Join(" ", row.ItemArray.Select(c => c?.ToString() ?? string.Empty)).Trim();

            if (string.IsNullOrWhiteSpace(result.BankName))
            {
                result.BankName = DetectBankNameFromText(rowText);
            }

            if (string.IsNullOrWhiteSpace(result.AccountNumber))
            {
                var acMatch = Regex.Match(rowText, @"(?:A/?c|Account|Acct)[\s\.:#No]*([0-9]{9,18})", RegexOptions.IgnoreCase);
                if (acMatch.Success)
                    result.AccountNumber = acMatch.Groups[1].Value;
            }

            // Check if this row is the table header
            int dCol = -1, descCol = -1, drCol = -1, crCol = -1, balCol = -1, refCol = -1, valDateCol = -1;
            for (int c = 0; c < table.Columns.Count; c++)
            {
                var val = row[c]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(val)) continue;

                var vLower = val.ToLowerInvariant();
                if (vLower.Contains("value date") || vLower.Contains("val date") || vLower.Contains("value dt") || vLower.Contains("val dt"))
                    valDateCol = c;
                else if (dCol == -1 && (vLower.Contains("date") || vLower.Contains("txn date") || vLower.Contains("tran date") || vLower.Equals("dt")))
                    dCol = c;
                else if (descCol == -1 && (vLower.Contains("particular") || vLower.Contains("description") || vLower.Contains("narration") || vLower.Contains("remarks")))
                    descCol = c;
                else if (refCol == -1 && (vLower.Contains("chq") || vLower.Contains("cheque") || vLower.Contains("ref") || vLower.Contains("utr") || vLower.Contains("trans id")))
                    refCol = c;
                else if (drCol == -1 && (vLower.Contains("debit") || vLower.Contains("withdrawal") || vLower.Contains("dr amt") || vLower.Equals("dr") || vLower.Equals("debit (dr)")))
                    drCol = c;
                else if (crCol == -1 && (vLower.Contains("credit") || vLower.Contains("deposit") || vLower.Contains("cr amt") || vLower.Equals("cr") || vLower.Equals("credit (cr)")))
                    crCol = c;
                else if (balCol == -1 && (vLower.Contains("balance") || vLower.Contains("closing") || vLower.Contains("running bal")))
                    balCol = c;
            }

            if (dCol != -1 && (drCol != -1 || crCol != -1))
            {
                headerRowIndex = r;
                colDate = dCol;
                colValueDate = valDateCol;
                colDesc = descCol != -1 ? descCol : (dCol + 1 < table.Columns.Count ? dCol + 1 : 0);
                colRef = refCol;
                colDebit = drCol;
                colCredit = crCol;
                colBalance = balCol;
                break;
            }
        }

        if (headerRowIndex == -1)
            return result;

        // 2. Parse Transactions
        int rowNumber = 1;
        for (int r = headerRowIndex + 1; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var dateObj = colDate >= 0 ? row[colDate] : null;

            if (!TryParseFlexibleDate(dateObj, out var txnDate))
                continue;

            var desc = colDesc >= 0 ? row[colDesc]?.ToString()?.Trim() ?? string.Empty : string.Empty;
            var descLower = desc.ToLowerInvariant();

            var debit = colDebit >= 0 ? ParseDecimal(row[colDebit]) : 0m;
            var credit = colCredit >= 0 ? ParseDecimal(row[colCredit]) : 0m;
            var balance = colBalance >= 0 ? ParseDecimal(row[colBalance]) : 0m;

            // Skip summary rows
            if (descLower.Contains("opening balance") ||
                descLower.Contains("closing balance") ||
                descLower.Contains("brought forward") ||
                descLower.Contains("carried forward") ||
                descLower.Contains("page ") ||
                descLower.Contains("end of statement") ||
                (descLower.Contains("total") && debit == 0 && credit == 0))
            {
                continue;
            }

            DateTime? valueDate = null;
            if (colValueDate >= 0 && TryParseFlexibleDate(row[colValueDate], out var vd))
                valueDate = vd;

            // Extract reference number
            string refNo = string.Empty;
            if (colRef >= 0)
                refNo = row[colRef]?.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(refNo))
            {
                var refMatch = Regex.Match(desc, @"(?:UPI|NEFT|RTGS|IMPS|CHQ|REF|UTR)[/:.\s-]*([A-Za-z0-9]+)", RegexOptions.IgnoreCase);
                if (refMatch.Success)
                    refNo = refMatch.Groups[1].Value;
                else
                {
                    var numMatch = Regex.Match(desc, @"\b([0-9]{6,16})\b");
                    if (numMatch.Success)
                        refNo = numMatch.Groups[1].Value;
                }
            }

            result.Transactions.Add(new BankStatementTransaction
            {
                RowNo = rowNumber++,
                Date = txnDate,
                ValueDate = valueDate,
                Description = desc,
                ReferenceNo = refNo,
                WithdrawalDebit = debit,
                DepositCredit = credit,
                Balance = balance
            });
        }

        if (result.Transactions.Count > 0)
        {
            result.FromDate = result.Transactions.Min(t => t.Date);
            result.ToDate = result.Transactions.Max(t => t.Date);
            result.ClosingBalance = result.Transactions.Last().Balance;

            // Infer opening balance if balance is available
            var first = result.Transactions.First();
            result.OpeningBalance = first.Balance + first.WithdrawalDebit - first.DepositCredit;
        }

        return result;
    }

    private static string DetectBankNameFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var upper = text.ToUpperInvariant();
        foreach (var bank in KnownBanks)
        {
            if (upper.Contains(bank))
                return bank;
        }
        return string.Empty;
    }

    private static bool TryParseFlexibleDate(object? value, out DateTime result)
    {
        result = default;
        if (value is null or DBNull) return false;
        if (value is DateTime dt)
        {
            result = dt.Date;
            return true;
        }
        if (value is double d and > 30000 and < 60000)
        {
            try
            {
                result = DateTime.FromOADate(d).Date;
                return true;
            }
            catch { }
        }

        var text = value.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return false;

        string[] formats =
        [
            "dd/MM/yyyy", "dd-MM-yyyy", "d/M/yyyy", "d-M-yyyy",
            "yyyy-MM-dd", "yyyy/MM/dd",
            "dd-MMM-yyyy", "dd/MMM/yyyy", "d-MMM-yyyy",
            "MM/dd/yyyy", "MM-dd-yyyy",
            "dd/MM/yy", "dd-MM-yy", "yyyyMMdd",
            "dd/MM/yyyy HH:mm:ss", "dd-MM-yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss",
            "dd/MM/yyyy hh:mm:ss tt", "dd-MM-yyyy hh:mm:ss tt"
        ];

        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;

        var dateOnly = text.Split(' ', 'T')[0];
        if (DateTime.TryParseExact(dateOnly, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;

        if (DateTime.TryParse(dateOnly, CultureInfo.CurrentCulture, DateTimeStyles.None, out result))
            return true;

        return DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out result);
    }

    private static decimal ParseDecimal(object? value)
    {
        if (value is null or DBNull) return 0m;
        if (value is decimal d) return d;
        if (value is double db) return (decimal)db;
        if (value is int i) return i;

        var str = value.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(str)) return 0m;

        // Clean commas and currency symbols
        str = Regex.Replace(str, @"[^\d.\-]", "");
        if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
            return Math.Abs(dec);

        return 0m;
    }
}
