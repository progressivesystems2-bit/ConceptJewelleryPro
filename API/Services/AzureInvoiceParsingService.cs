using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using ConceptJewelleryPro.API.Models;

namespace ConceptJewelleryPro.API.Services;

public sealed class AzureInvoiceParsingService : IInvoiceParsingService
{
    private readonly DocumentAnalysisClient _client;
    private readonly ILogger<AzureInvoiceParsingService> _logger;

    public AzureInvoiceParsingService(IConfiguration configuration, ILogger<AzureInvoiceParsingService> logger)
    {
        _logger = logger;

        var endpoint = configuration["AzureDocumentIntelligence:Endpoint"]
            ?? throw new InvalidOperationException("AzureDocumentIntelligence:Endpoint is not configured.");
        var apiKey = configuration["AzureDocumentIntelligence:ApiKey"]
            ?? throw new InvalidOperationException("AzureDocumentIntelligence:ApiKey is not configured.");

        _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<InvoiceResult> ParseInvoiceAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing invoice from image: {ImagePath}", imagePath);

        await using var stream = File.OpenRead(imagePath);

        var operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-invoice",
            stream,
            cancellationToken: cancellationToken);

        var result = operation.Value;
        var invoiceResult = new InvoiceResult
        {
            SourceFilePath = imagePath
        };

        if (result.Documents.Count == 0)
        {
            _logger.LogWarning("No invoice documents found in image: {ImagePath}", imagePath);
            return invoiceResult;
        }

        var invoice = result.Documents[0];

        invoiceResult.VendorName = GetFieldValueString(invoice, "VendorName");
        invoiceResult.VendorAddress = GetFieldValueString(invoice, "VendorAddress");
        invoiceResult.InvoiceNumber = GetFieldValueString(invoice, "InvoiceId");
        invoiceResult.InvoiceDate = GetFieldValueDate(invoice, "InvoiceDate");
        invoiceResult.DueDate = GetFieldValueDate(invoice, "DueDate");
        invoiceResult.PurchaseOrderNumber = GetFieldValueString(invoice, "PurchaseOrder");
        invoiceResult.CustomerName = GetFieldValueString(invoice, "CustomerName");
        invoiceResult.SubTotal = GetFieldValueDecimal(invoice, "SubTotal");
        invoiceResult.TotalTax = GetFieldValueDecimal(invoice, "TotalTax");
        invoiceResult.TotalAmount = GetFieldValueDecimal(invoice, "InvoiceTotal");
        invoiceResult.CurrencyCode = GetCurrencyCode(invoice, "InvoiceTotal");

        if (invoice.Fields.TryGetValue("Items", out var itemsField) &&
            itemsField.FieldType == DocumentFieldType.List)
        {
            foreach (var itemField in itemsField.Value.AsList())
            {
                if (itemField.FieldType != DocumentFieldType.Dictionary)
                    continue;

                var fields = itemField.Value.AsDictionary();
                var lineItem = new InvoiceLineItem
                {
                    Description = GetDictFieldString(fields, "Description"),
                    Quantity = GetDictFieldDecimal(fields, "Quantity"),
                    UnitPrice = GetDictFieldDecimal(fields, "UnitPrice"),
                    Amount = GetDictFieldDecimal(fields, "Amount"),
                    Unit = GetDictFieldString(fields, "Unit"),
                    Tax = GetDictFieldDecimal(fields, "Tax")
                };

                invoiceResult.Items.Add(lineItem);
            }
        }

        _logger.LogInformation(
            "Successfully parsed invoice {InvoiceNumber} with {ItemCount} line items",
            invoiceResult.InvoiceNumber,
            invoiceResult.Items.Count);

        return invoiceResult;
    }

    private static string? GetFieldValueString(AnalyzedDocument document, string fieldName)
    {
        if (document.Fields.TryGetValue(fieldName, out var field))
        {
            return field.FieldType switch
            {
                DocumentFieldType.String => field.Value.AsString(),
                DocumentFieldType.Address => field.Content,
                _ => field.Content
            };
        }
        return null;
    }

    private static string? GetFieldValueDate(AnalyzedDocument document, string fieldName)
    {
        if (document.Fields.TryGetValue(fieldName, out var field) &&
            field.FieldType == DocumentFieldType.Date)
        {
            return field.Value.AsDate().ToString("yyyy-MM-dd");
        }
        return null;
    }

    private static decimal? GetFieldValueDecimal(AnalyzedDocument document, string fieldName)
    {
        if (document.Fields.TryGetValue(fieldName, out var field) &&
            field.FieldType == DocumentFieldType.Currency)
        {
            return (decimal)field.Value.AsCurrency().Amount;
        }
        return null;
    }

    private static string? GetCurrencyCode(AnalyzedDocument document, string fieldName)
    {
        if (document.Fields.TryGetValue(fieldName, out var field) &&
            field.FieldType == DocumentFieldType.Currency)
        {
            return field.Value.AsCurrency().Code;
        }
        return null;
    }

    private static string? GetDictFieldString(IReadOnlyDictionary<string, DocumentField> fields, string key)
    {
        if (fields.TryGetValue(key, out var field))
        {
            return field.FieldType == DocumentFieldType.String
                ? field.Value.AsString()
                : field.Content;
        }
        return null;
    }

    private static decimal? GetDictFieldDecimal(IReadOnlyDictionary<string, DocumentField> fields, string key)
    {
        if (fields.TryGetValue(key, out var field))
        {
            return field.FieldType switch
            {
                DocumentFieldType.Currency => (decimal)field.Value.AsCurrency().Amount,
                DocumentFieldType.Double => (decimal)field.Value.AsDouble(),
                _ => null
            };
        }
        return null;
    }
}
