using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Shared;
using ExcelDataReader;

namespace ConceptJewelleryPro.WPF.Services;

public sealed class CustomerContactImportResult
{
    public List<Customer> AddedCustomers { get; } = new();
    public List<Customer> UpdatedCustomers { get; } = new();
    public int SkippedCount { get; set; }
    public List<string> ErrorMessages { get; } = new();
    public int TotalProcessed => AddedCustomers.Count + UpdatedCustomers.Count + SkippedCount;
}

public static class CustomerContactImportService
{
    static CustomerContactImportService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string GenerateCsvTemplate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("CustomerCode,Initial,CustomerName,Mobile,Phone,Email,Address,Address2,City,Pincode,Group,DOB,DOA,Profession");
        sb.AppendLine("CUST001,Mr.,Rahul Sharma,9876543210,0221234567,rahul.sharma@example.com,Flat 101 Lotus Apts,MG Road,Mumbai,400001,Retail,15/08/1985,25/11/2012,Business");
        sb.AppendLine("CUST002,Mrs.,Pooja Patel,9823456789,,pooja.patel@example.com,45 Diamond Park,Ring Road,Surat,395001,VIP,10/04/1990,12/12/2015,Doctor");
        sb.AppendLine("CUST003,M/s,Shree Jewellers,9811223344,0112345678,info@shreejewellers.com,77 Zaveri Bazar,,Surat,395002,Wholesale,,,Trader");
        return sb.ToString();
    }

    public static CustomerContactImportResult ParseFile(
        string filePath,
        string currentUser,
        IReadOnlyCollection<Customer> existingCustomers)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("Contacts file not found.", filePath);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var existingByCode = existingCustomers
            .Where(c => !string.IsNullOrWhiteSpace(c.Code))
            .ToDictionary(c => c.Code.Trim(), c => c, StringComparer.OrdinalIgnoreCase);

        var existingByMobile = existingCustomers
            .Where(c => !string.IsNullOrWhiteSpace(c.Mobile))
            .ToDictionary(c => NormalizeMobile(c.Mobile), c => c, StringComparer.OrdinalIgnoreCase);

        var usedCodes = new HashSet<string>(existingByCode.Keys, StringComparer.OrdinalIgnoreCase);

        var table = LoadDataTable(filePath);
        if (table == null || table.Rows.Count == 0)
        {
            var emptyResult = new CustomerContactImportResult();
            emptyResult.ErrorMessages.Add("The selected file does not contain any data rows.");
            return emptyResult;
        }

        return ProcessDataTable(table, currentUser, existingByCode, existingByMobile, usedCodes);
    }

    private static DataTable? LoadDataTable(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (ext == ".csv")
            {
                using var reader = ExcelReaderFactory.CreateCsvReader(stream);
                var ds = reader.AsDataSet();
                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    return ds.Tables[0];
                }
            }
            else
            {
                using var reader = ExcelReaderFactory.CreateReader(stream);
                var ds = reader.AsDataSet();
                if (ds.Tables.Count > 0)
                {
                    DataTable target = ds.Tables[0];
                    foreach (DataTable t in ds.Tables)
                    {
                        if (t.Rows.Count > target.Rows.Count)
                            target = t;
                    }
                    return target;
                }
            }
        }
        catch
        {
            if (ext == ".csv")
            {
                return LoadCsvDirect(filePath);
            }
            throw;
        }

        if (ext == ".csv")
        {
            return LoadCsvDirect(filePath);
        }

        return null;
    }

    private static DataTable LoadCsvDirect(string filePath)
    {
        var dt = new DataTable();
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length == 0) return dt;

        bool columnsCreated = false;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = ParseCsvLine(line);
            if (!columnsCreated)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    dt.Columns.Add($"Col_{i}");
                }
                columnsCreated = true;
            }

            var row = dt.NewRow();
            for (int i = 0; i < Math.Min(parts.Length, dt.Columns.Count); i++)
            {
                row[i] = parts[i];
            }
            dt.Rows.Add(row);
        }
        return dt;
    }

    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        values.Add(current.ToString());
        return values.ToArray();
    }

    private static CustomerContactImportResult ProcessDataTable(
        DataTable table,
        string currentUser,
        Dictionary<string, Customer> existingByCode,
        Dictionary<string, Customer> existingByMobile,
        HashSet<string> usedCodes)
    {
        var result = new CustomerContactImportResult();

        // 1. Locate Header Row
        int headerRowIndex = -1;
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int scanMax = Math.Min(20, table.Rows.Count);
        for (int r = 0; r < scanMax; r++)
        {
            var tempMap = MatchHeaders(table.Rows[r]);
            if (tempMap.ContainsKey("CustomerName") || tempMap.ContainsKey("Mobile"))
            {
                headerRowIndex = r;
                colMap = tempMap;
                break;
            }
        }

        if (headerRowIndex == -1)
        {
            result.ErrorMessages.Add("Could not detect column headers. The file must contain at least a 'CustomerName' or 'Mobile' column.");
            return result;
        }

        int nextCodeSeq = CalculateNextCodeSequence(usedCodes);

        // 2. Process Data Rows
        for (int r = headerRowIndex + 1; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];

            string code = GetColVal(row, colMap, "CustomerCode");
            string initial = GetColVal(row, colMap, "Initial");
            string name = GetColVal(row, colMap, "CustomerName");
            string mobile = GetColVal(row, colMap, "Mobile");
            string phone = GetColVal(row, colMap, "Phone");
            string email = GetColVal(row, colMap, "Email");
            string addr1 = GetColVal(row, colMap, "Address");
            string addr2 = GetColVal(row, colMap, "Address2");
            string city = GetColVal(row, colMap, "City");
            string pin = GetColVal(row, colMap, "Pincode");
            string group = GetColVal(row, colMap, "Group");
            string dobStr = GetColVal(row, colMap, "DOB");
            string doaStr = GetColVal(row, colMap, "DOA");
            string profession = GetColVal(row, colMap, "Profession");
            string nosStr = GetColVal(row, colMap, "Nos");

            // Ignore completely blank row
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(mobile) && string.IsNullOrWhiteSpace(code))
            {
                result.SkippedCount++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                if (!string.IsNullOrWhiteSpace(mobile))
                {
                    name = "Customer " + mobile;
                }
                else
                {
                    result.SkippedCount++;
                    continue;
                }
            }

            // Autofill City from 6-digit Pincode if empty
            if (string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(pin))
            {
                var cleanPin = new string(pin.Where(char.IsDigit).ToArray());
                if (cleanPin.Length == 6)
                {
                    var lookup = PincodeService.Lookup(cleanPin);
                    if (lookup.HasValue && !string.IsNullOrWhiteSpace(lookup.Value.City))
                    {
                        city = lookup.Value.City;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(group))
            {
                group = "Retail";
            }

            DateTime? dob = ParseDate(row, colMap, "DOB", dobStr);
            DateTime? doa = ParseDate(row, colMap, "DOA", doaStr);

            int.TryParse(nosStr, out int nos);

            // Check if existing customer matches by Code or Mobile
            Customer? existing = null;
            if (!string.IsNullOrWhiteSpace(code) && existingByCode.TryGetValue(code.Trim(), out var byCode))
            {
                existing = byCode;
            }
            else if (!string.IsNullOrWhiteSpace(mobile))
            {
                var normMob = NormalizeMobile(mobile);
                if (existingByMobile.TryGetValue(normMob, out var byMob))
                {
                    existing = byMob;
                }
            }

            if (existing != null)
            {
                // Update existing record
                if (!string.IsNullOrWhiteSpace(initial)) existing.Initial = initial;
                if (!string.IsNullOrWhiteSpace(name)) existing.CompanyName = name;
                if (!string.IsNullOrWhiteSpace(addr1)) existing.Address = addr1;
                if (!string.IsNullOrWhiteSpace(addr2)) existing.Address2 = addr2;
                if (!string.IsNullOrWhiteSpace(city)) existing.City = city;
                if (!string.IsNullOrWhiteSpace(pin)) existing.Pincode = pin;
                if (!string.IsNullOrWhiteSpace(group)) existing.Group = group;
                if (!string.IsNullOrWhiteSpace(phone)) existing.Phone = phone;
                if (!string.IsNullOrWhiteSpace(mobile)) existing.Mobile = mobile;
                if (!string.IsNullOrWhiteSpace(email)) existing.Email = email;
                if (!string.IsNullOrWhiteSpace(profession)) existing.Profession = profession;
                if (dob.HasValue) existing.DOB = dob;
                if (doa.HasValue) existing.DOA = doa;
                if (nos > 0) existing.Nos = nos;
                existing.User = currentUser;

                result.UpdatedCustomers.Add(existing);
            }
            else
            {
                // Create brand new customer
                if (string.IsNullOrWhiteSpace(code))
                {
                    code = GenerateUniqueCode(usedCodes, ref nextCodeSeq);
                }
                else if (usedCodes.Contains(code))
                {
                    code = GenerateUniqueCode(usedCodes, ref nextCodeSeq);
                }

                usedCodes.Add(code);

                var newCust = new Customer
                {
                    Code = code,
                    Initial = !string.IsNullOrWhiteSpace(initial) ? initial : "Mr.",
                    CompanyName = name,
                    Address = addr1,
                    Address2 = addr2,
                    City = city,
                    Pincode = pin,
                    Group = group,
                    Phone = phone,
                    Mobile = mobile,
                    Email = email,
                    DOB = dob,
                    DOA = doa,
                    Profession = profession,
                    Nos = nos,
                    Print = true,
                    Status = "Saved",
                    Saved = "Yes",
                    User = currentUser,
                    Date = DateTime.Today,
                    Time = DateTime.Now.TimeOfDay
                };

                result.AddedCustomers.Add(newCust);
                existingByCode[code] = newCust;
                if (!string.IsNullOrWhiteSpace(mobile))
                {
                    existingByMobile[NormalizeMobile(mobile)] = newCust;
                }
            }
        }

        return result;
    }

    private static Dictionary<string, int> MatchHeaders(DataRow row)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int c = 0; c < row.Table.Columns.Count; c++)
        {
            var header = row[c]?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(header)) continue;

            var clean = Regex.Replace(header, @"[\s_\-\.]+", "").ToLowerInvariant();

            if (clean is "customercode" or "code" or "custcode" or "id")
                SetIfAbsent(map, "CustomerCode", c);
            else if (clean is "initial" or "salutation" or "title" or "prefix")
                SetIfAbsent(map, "Initial", c);
            else if (clean is "customername" or "companyname" or "name" or "partyname" or "contactperson" or "clientname" or "fullname")
                SetIfAbsent(map, "CustomerName", c);
            else if (clean is "mobile" or "mobileno" or "mobilenumber" or "cell" or "cellno" or "contactno" or "phone1")
                SetIfAbsent(map, "Mobile", c);
            else if (clean is "phone" or "phoneno" or "telephone" or "tel" or "landline" or "phone2" or "altmobile")
                SetIfAbsent(map, "Phone", c);
            else if (clean is "email" or "emailid" or "emailaddress" or "mail")
                SetIfAbsent(map, "Email", c);
            else if (clean is "address" or "address1" or "street" or "addressline1")
                SetIfAbsent(map, "Address", c);
            else if (clean is "address2" or "area" or "locality" or "addressline2")
                SetIfAbsent(map, "Address2", c);
            else if (clean is "city" or "town" or "location" or "district")
                SetIfAbsent(map, "City", c);
            else if (clean is "pincode" or "pincodeno" or "pin" or "zip" or "zipcode" or "postalcode")
                SetIfAbsent(map, "Pincode", c);
            else if (clean is "group" or "customergroup" or "category" or "customercategory")
                SetIfAbsent(map, "Group", c);
            else if (clean is "dob" or "dateofbirth" or "birthdate" or "birthday")
                SetIfAbsent(map, "DOB", c);
            else if (clean is "doa" or "dateofanniversary" or "anniversary" or "anniversarydate")
                SetIfAbsent(map, "DOA", c);
            else if (clean is "profession" or "occupation" or "job" or "work")
                SetIfAbsent(map, "Profession", c);
            else if (clean is "nos" or "members" or "familymembers")
                SetIfAbsent(map, "Nos", c);
        }

        return map;
    }

    private static void SetIfAbsent(Dictionary<string, int> map, string key, int colIdx)
    {
        if (!map.ContainsKey(key))
        {
            map[key] = colIdx;
        }
    }

    private static string GetColVal(DataRow row, Dictionary<string, int> colMap, string key)
    {
        if (!colMap.TryGetValue(key, out int colIdx) || colIdx < 0 || colIdx >= row.Table.Columns.Count)
            return string.Empty;

        var val = row[colIdx];
        if (val == null || val is DBNull) return string.Empty;

        return val.ToString()?.Trim() ?? string.Empty;
    }

    private static DateTime? ParseDate(DataRow row, Dictionary<string, int> colMap, string key, string text)
    {
        if (!colMap.TryGetValue(key, out int colIdx) || colIdx < 0 || colIdx >= row.Table.Columns.Count)
            return null;

        var rawObj = row[colIdx];
        if (rawObj is DateTime dt)
            return dt.Date;

        if (string.IsNullOrWhiteSpace(text))
            return null;

        string[] formats =
        [
            "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
            "yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy", "M/d/yyyy"
        ];

        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed.Date;

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallback))
            return fallback.Date;

        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var localParsed))
            return localParsed.Date;

        return null;
    }

    private static string NormalizeMobile(string mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return string.Empty;
        var digits = new string(mobile.Where(char.IsDigit).ToArray());
        return digits.Length >= 10 ? digits.Substring(digits.Length - 10) : digits;
    }

    private static int CalculateNextCodeSequence(HashSet<string> usedCodes)
    {
        int max = 0;
        foreach (var code in usedCodes)
        {
            var match = Regex.Match(code, @"(?:CUST|C)?([0-9]+)$", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
            {
                if (num > max) max = num;
            }
        }
        return max + 1;
    }

    private static string GenerateUniqueCode(HashSet<string> usedCodes, ref int nextCodeSeq)
    {
        while (true)
        {
            var candidate = $"CUST{nextCodeSeq:D4}";
            nextCodeSeq++;
            if (!usedCodes.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
