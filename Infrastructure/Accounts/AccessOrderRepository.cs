using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessOrderRepository : IGstOrderRepositoryFallback, IOrderRepository
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public AccessOrderRepository(string databasePath)
    {
        _databasePath = databasePath;
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};Persist Security Info=False;";
        EnsureTableExists();
    }

    private void EnsureTableExists()
    {
        try
        {
            using var conn = new OleDbConnection(_connectionString);
            conn.Open();
            var schema = conn.GetSchema("Tables");
            bool exists = false;
            foreach (DataRow row in schema.Rows)
            {
                if (string.Equals(row["TABLE_NAME"]?.ToString(), "Order", StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                var sql = @"CREATE TABLE [Order] (
                    [OrderNo] VARCHAR(50) CONSTRAINT PK_Order PRIMARY KEY,
                    [Nos] INT,
                    [OrderDate] DATETIME,
                    [DeliveryDate] DATETIME,
                    [CustomerName] VARCHAR(100),
                    [Address] VARCHAR(150),
                    [Address2] VARCHAR(150),
                    [City] VARCHAR(50),
                    [Phone] VARCHAR(50),
                    [ItemName] VARCHAR(100),
                    [Qty] DECIMAL(18,3),
                    [Size] VARCHAR(50),
                    [Description] LONGTEXT,
                    [Purity] VARCHAR(50),
                    [AppWeight] DECIMAL(18,3),
                    [MakPerGr] DECIMAL(18,2),
                    [Making] DECIMAL(18,2),
                    [MetalRate] DECIMAL(18,2),
                    [AppValue] DECIMAL(18,2),
                    [RateType] VARCHAR(50),
                    [Photo] VARCHAR(255),
                    [EntryUser] VARCHAR(50),
                    [EntryDate] DATETIME,
                    [EntryTime] DATETIME,
                    [Status] VARCHAR(50),
                    [ComputerName] VARCHAR(50),
                    [Closed] YESNO,
                    [StoneWt] DECIMAL(18,3),
                    [StoneValue] DECIMAL(18,2),
                    [StoneName] VARCHAR(50),
                    [KarigarName] VARCHAR(100),
                    [Company] VARCHAR(100),
                    [StyleNo] VARCHAR(50),
                    [CCode] VARCHAR(50),
                    [CompanyPerson] VARCHAR(50),
                    [OrderDeliveryDate] DATETIME,
                    [AdvancePaid] DECIMAL(18,2),
                    [OrderCategory] VARCHAR(50)
                );";
                using var cmd = new OleDbCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            else
            {
                AccessOrderSchemaMigrator.EnsureSchemaSync(_connectionString);
            }
        }
        catch
        {
            // Ignore if already exists or permission issue
        }
    }

    public async Task<IReadOnlyList<CustomerOrder>> GetAllAsync(string? savePath = null)
    {
        var list = new List<CustomerOrder>();

        await Task.Run(() =>
        {
            using var conn = new OleDbConnection(_connectionString);
            conn.Open();

            var sql = "SELECT * FROM [Order] ORDER BY [OrderDate] DESC, [Nos] DESC, [OrderNo] DESC";
            using var cmd = new OleDbCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var order = MapOrder(reader, savePath);
                list.Add(order);
            }
        });

        return list;
    }

    public async Task<CustomerOrder?> GetByOrderNoAsync(string orderNo, string? savePath = null)
    {
        CustomerOrder? order = null;

        await Task.Run(() =>
        {
            using var conn = new OleDbConnection(_connectionString);
            conn.Open();

            var sql = "SELECT * FROM [Order] WHERE [OrderNo] = ?";
            using var cmd = new OleDbCommand(sql, conn);
            cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = orderNo;

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                order = MapOrder(reader, savePath);
            }
        });

        return order;
    }

    public async Task<string> GetNextOrderNoAsync()
    {
        int nextNos = await GetNextNosAsync();
        int fyStart = DateTime.Today.Month >= 4 ? DateTime.Today.Year : DateTime.Today.Year - 1;
        string fy = $"{fyStart % 100:D2}-{(fyStart + 1) % 100:D2}";
        return $"OR{nextNos:D5}/{fy}";
    }

    public async Task<int> GetNextNosAsync()
    {
        int maxNos = 0;

        await Task.Run(() =>
        {
            using var conn = new OleDbConnection(_connectionString);
            conn.Open();

            var sql = "SELECT MAX([Nos]) FROM [Order]";
            using var cmd = new OleDbCommand(sql, conn);
            var val = cmd.ExecuteScalar();
            if (val != null && val != DBNull.Value)
            {
                maxNos = Convert.ToInt32(val);
            }
        });

        return maxNos + 1;
    }

    public async Task<bool> SaveAsync(CustomerOrder order, bool isNew, string currentUser)
    {
        AccessOrderSchemaMigrator.EnsureSchemaSync(_connectionString);

        return await Task.Run(() =>
        {
            using var conn = new OleDbConnection(_connectionString);
            conn.Open();

            if (isNew)
            {
                if (order.Nos <= 0)
                {
                    var maxCmd = new OleDbCommand("SELECT MAX([Nos]) FROM [Order]", conn);
                    var maxVal = maxCmd.ExecuteScalar();
                    order.Nos = (maxVal != null && maxVal != DBNull.Value) ? Convert.ToInt32(maxVal) + 1 : 1;
                }

                if (string.IsNullOrWhiteSpace(order.OrderNo))
                {
                    order.OrderNo = $"ORD-{order.Nos:D4}";
                }

                var sql = @"INSERT INTO [Order] (
                    [OrderNo], [Nos], [OrderDate], [DeliveryDate], [CustomerName],
                    [Address], [Address2], [City], [Phone], [ItemName],
                    [Qty], [Size], [Description], [Purity], [AppWeight],
                    [MakPerGr], [Making], [MetalRate], [AppValue], [RateType],
                    [Photo], [EntryUser], [EntryDate], [EntryTime], [Status],
                    [ComputerName], [Closed], [StoneWt], [StoneValue], [StoneName],
                    [KarigarName], [Company], [StyleNo], [CCode], [CompanyPerson],
                    [OrderDeliveryDate], [AdvancePaid], [OrderCategory]
                ) VALUES (
                    ?, ?, ?, ?, ?,
                    ?, ?, ?, ?, ?,
                    ?, ?, ?, ?, ?,
                    ?, ?, ?, ?, ?,
                    ?, ?, ?, ?, ?,
                    ?, ?, ?, ?, ?,
                    ?, ?, ?, ?, ?,
                    ?, ?, ?
                )";

                using var cmd = new OleDbCommand(sql, conn);
                AddParameters(cmd, order, currentUser, isNew: true);
                return cmd.ExecuteNonQuery() > 0;
            }
            else
            {
                var sql = @"UPDATE [Order] SET
                    [OrderDate] = ?, [DeliveryDate] = ?, [CustomerName] = ?,
                    [Address] = ?, [Address2] = ?, [City] = ?, [Phone] = ?, [ItemName] = ?,
                    [Qty] = ?, [Size] = ?, [Description] = ?, [Purity] = ?, [AppWeight] = ?,
                    [MakPerGr] = ?, [Making] = ?, [MetalRate] = ?, [AppValue] = ?, [RateType] = ?,
                    [Photo] = ?, [Status] = ?, [Closed] = ?, [StoneWt] = ?, [StoneValue] = ?,
                    [StoneName] = ?, [KarigarName] = ?, [Company] = ?, [StyleNo] = ?, [CCode] = ?,
                    [CompanyPerson] = ?, [OrderDeliveryDate] = ?, [AdvancePaid] = ?, [OrderCategory] = ?
                    WHERE [OrderNo] = ?";

                using var cmd = new OleDbCommand(sql, conn);
                AddParameters(cmd, order, currentUser, isNew: false);
                cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = order.OrderNo;
                return cmd.ExecuteNonQuery() > 0;
            }
        });
    }

    public async Task<bool> DeleteAsync(string orderNo, string currentUser)
    {
        return await Task.Run(() =>
        {
            using var conn = new OleDbConnection(_connectionString);
            conn.Open();

            var sql = "DELETE FROM [Order] WHERE [OrderNo] = ?";
            using var cmd = new OleDbCommand(sql, conn);
            cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = orderNo;
            return cmd.ExecuteNonQuery() > 0;
        });
    }

    public async Task<bool> UpdateStatusAsync(string orderNo, string status, bool closed, string currentUser)
    {
        return await Task.Run(() =>
        {
            using var conn = new OleDbConnection(_connectionString);
            conn.Open();

            var sql = "UPDATE [Order] SET [Status] = ?, [Closed] = ? WHERE [OrderNo] = ?";
            using var cmd = new OleDbCommand(sql, conn);
            cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = status;
            cmd.Parameters.Add("?", OleDbType.Boolean).Value = closed;
            cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = orderNo;
            return cmd.ExecuteNonQuery() > 0;
        });
    }

    private static void AddParameters(OleDbCommand cmd, CustomerOrder o, string currentUser, bool isNew)
    {
        if (isNew)
        {
            cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = o.OrderNo ?? string.Empty;
            cmd.Parameters.Add("?", OleDbType.Integer).Value = o.Nos;
        }

        cmd.Parameters.Add("?", OleDbType.Date).Value = o.OrderDate;
        cmd.Parameters.Add("?", OleDbType.Date).Value = (object?)o.DeliveryDate ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 100).Value = o.CustomerName ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 150).Value = o.Address ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 150).Value = o.Address2 ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = o.City ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = o.Phone ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 100).Value = o.ItemName ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.Decimal).Value = o.Qty;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = o.Size ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.LongVarWChar).Value = o.Description ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = o.Purity ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.Decimal).Value = o.AppWeight;
        cmd.Parameters.Add("?", OleDbType.Decimal).Value = o.MakPerGr;
        cmd.Parameters.Add("?", OleDbType.Decimal).Value = o.Making;
        cmd.Parameters.Add("?", OleDbType.Decimal).Value = o.MetalRate;
        cmd.Parameters.Add("?", OleDbType.Decimal).Value = o.AppValue;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = o.RateType ?? "Fix";
        cmd.Parameters.Add("?", OleDbType.VarWChar, 255).Value = o.Photo ?? string.Empty;

        if (isNew)
        {
            cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = currentUser ?? Environment.UserName;
            cmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Today;
            cmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
        }

        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = o.Status ?? "Pending";

        if (isNew)
        {
            cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = Environment.MachineName;
        }

        cmd.Parameters.Add("?", OleDbType.Boolean).Value = o.Closed;
        cmd.Parameters.Add("?", OleDbType.Decimal).Value = o.StoneWt;
        cmd.Parameters.Add("?", OleDbType.Decimal).Value = o.StoneValue;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = o.StoneName ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 100).Value = o.KarigarName ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 100).Value = o.Company ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = o.StyleNo ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = o.CCode ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = o.CompanyPerson ?? string.Empty;
        cmd.Parameters.Add("?", OleDbType.Date).Value = (object?)o.OrderDeliveryDate ?? (object?)o.DeliveryDate ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Decimal).Value = o.AdvancePaid;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = string.IsNullOrWhiteSpace(o.OrderCategory) ? "Order" : o.OrderCategory;
    }

    private static CustomerOrder MapOrder(OleDbDataReader r, string? savePath)
    {
        var o = new CustomerOrder
        {
            OrderNo = r["OrderNo"]?.ToString() ?? string.Empty,
            OrderCategory = GetColumnStringSafe(r, "OrderCategory", "Order"),
            Nos = r["Nos"] != DBNull.Value ? Convert.ToInt32(r["Nos"]) : 0,
            OrderDate = r["OrderDate"] != DBNull.Value ? Convert.ToDateTime(r["OrderDate"]) : DateTime.Today,
            DeliveryDate = r["DeliveryDate"] != DBNull.Value ? Convert.ToDateTime(r["DeliveryDate"]) : null,
            CustomerName = r["CustomerName"]?.ToString() ?? string.Empty,
            Address = r["Address"]?.ToString() ?? string.Empty,
            Address2 = r["Address2"]?.ToString() ?? string.Empty,
            City = r["City"]?.ToString() ?? string.Empty,
            Phone = r["Phone"]?.ToString() ?? string.Empty,
            ItemName = r["ItemName"]?.ToString() ?? string.Empty,
            Qty = r["Qty"] != DBNull.Value ? Convert.ToDecimal(r["Qty"]) : 1m,
            Size = r["Size"]?.ToString() ?? string.Empty,
            Description = r["Description"]?.ToString() ?? string.Empty,
            Purity = r["Purity"]?.ToString() ?? string.Empty,
            AppWeight = r["AppWeight"] != DBNull.Value ? Convert.ToDecimal(r["AppWeight"]) : 0m,
            MakPerGr = r["MakPerGr"] != DBNull.Value ? Convert.ToDecimal(r["MakPerGr"]) : 0m,
            Making = r["Making"] != DBNull.Value ? Convert.ToDecimal(r["Making"]) : 0m,
            MetalRate = r["MetalRate"] != DBNull.Value ? Convert.ToDecimal(r["MetalRate"]) : 0m,
            AppValue = r["AppValue"] != DBNull.Value ? Convert.ToDecimal(r["AppValue"]) : 0m,
            RateType = r["RateType"]?.ToString() ?? "Fix",
            Photo = r["Photo"]?.ToString() ?? string.Empty,
            EntryUser = r["EntryUser"]?.ToString() ?? string.Empty,
            EntryDate = r["EntryDate"] != DBNull.Value ? Convert.ToDateTime(r["EntryDate"]) : null,
            EntryTime = r["EntryTime"] != DBNull.Value ? Convert.ToDateTime(r["EntryTime"]) : null,
            Status = r["Status"]?.ToString() ?? "Pending",
            ComputerName = r["ComputerName"]?.ToString() ?? string.Empty,
            Closed = r["Closed"] != DBNull.Value && Convert.ToBoolean(r["Closed"]),
            StoneWt = r["StoneWt"] != DBNull.Value ? Convert.ToDecimal(r["StoneWt"]) : 0m,
            StoneValue = r["StoneValue"] != DBNull.Value ? Convert.ToDecimal(r["StoneValue"]) : 0m,
            StoneName = r["StoneName"]?.ToString() ?? string.Empty,
            KarigarName = r["KarigarName"]?.ToString() ?? string.Empty,
            Company = r["Company"]?.ToString() ?? string.Empty,
            StyleNo = r["StyleNo"]?.ToString() ?? string.Empty,
            CCode = r["CCode"]?.ToString() ?? string.Empty,
            CompanyPerson = r["CompanyPerson"]?.ToString() ?? string.Empty,
            OrderDeliveryDate = r["OrderDeliveryDate"] != DBNull.Value ? Convert.ToDateTime(r["OrderDeliveryDate"]) : null,
            AdvancePaid = r["AdvancePaid"] != DBNull.Value ? Convert.ToDecimal(r["AdvancePaid"]) : 0m
        };

        o.ResolvedPhotoPath = ResolveOrderPhotoPath(o.Photo, o.OrderNo, savePath);
        return o;
    }

    private static string GetColumnStringSafe(OleDbDataReader r, string colName, string fallback)
    {
        try
        {
            var val = r[colName];
            if (val != null && val != DBNull.Value)
            {
                var s = val.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
        }
        catch { }
        return fallback;
    }

    public static string? ResolveOrderPhotoPath(string? photoField, string? orderNo, string? savePath)
    {
        // 1. If photoField is a direct absolute path that exists:
        if (!string.IsNullOrWhiteSpace(photoField) && File.Exists(photoField))
        {
            return photoField;
        }

        // 2. Check strictly inside savePath (SavePath\Order or SavePath\CustomerRequirements)
        if (!string.IsNullOrWhiteSpace(savePath) && Directory.Exists(savePath))
        {
            var searchDirs = new[]
            {
                Path.Combine(savePath, "Order"),
                Path.Combine(savePath, "CustomerRequirements")
            };

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;

                if (!string.IsNullOrWhiteSpace(photoField))
                {
                    var p1 = Path.Combine(dir, Path.GetFileName(photoField));
                    if (File.Exists(p1)) return p1;
                }

                if (!string.IsNullOrWhiteSpace(orderNo))
                {
                    var cleanNo = orderNo.Replace("/", "_").Replace("\\", "_");
                    var candidates = new[]
                    {
                        Path.Combine(dir, $"{cleanNo}.jpg"),
                        Path.Combine(dir, $"{cleanNo}.jpeg"),
                        Path.Combine(dir, $"{cleanNo}.png")
                    };
                    foreach (var c in candidates)
                    {
                        if (File.Exists(c)) return c;
                    }
                }
            }
        }

        return null;
    }
}

// Marker interface for fallback
public interface IGstOrderRepositoryFallback { }
