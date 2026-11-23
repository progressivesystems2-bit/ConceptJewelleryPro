using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ConceptJewelleryPro.Application.Abstractions;
using ConceptJewelleryPro.Application.Services;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class BankReconciliationViewModel : ViewModelBase
{
    private readonly IAccountRepository _accountRepository;
    private readonly ILedgerReportRepository _ledgerReportRepository;
    private readonly IBankStatementParserService _statementParser;
    private readonly Action<string, object?>? _navigateModule;

    private Account? _selectedBankAccount;
    private string _statementFilePath = string.Empty;
    private string _statementFileName = string.Empty;
    private DateTime _fromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
    private DateTime _toDate = DateTime.Today;
    private string _statusMessage = "Select a Bank Account and load an official Bank Statement to begin reconciliation.";
    private bool _isLoading;
    private bool _hasStatement;
    private string _selectedFilter = "All";
    private BankReconciliationItem? _selectedItem;

    private decimal _statementClosingBalance;
    private decimal _bookClosingBalance;
    private decimal _unclearedDeposits;
    private decimal _unpresentedWithdrawals;
    private decimal _reconciledBalance;
    private decimal _netDifference;
    private bool _isFullyReconciled;
    private int _totalStatementCount;
    private int _reconciledCount;
    private int _partialMatchCount;
    private int _bankOnlyCount;
    private int _erpOnlyCount;

    public ObservableCollection<Account> BankAccounts { get; } = [];
    public ObservableCollection<BankReconciliationItem> AllItems { get; } = [];
    public ObservableCollection<BankReconciliationItem> FilteredItems { get; } = [];
    public IReadOnlyList<string> FilterOptions { get; } = ["All", "Reconciled", "Partial Matches", "Bank Only", "ERP Only"];

    public RelayCommand BrowseStatementFileCommand { get; }
    public AsyncRelayCommand RunReconciliationCommand { get; }
    public RelayCommand AcceptPartialMatchCommand { get; }
    public RelayCommand UnlinkMatchedItemCommand { get; }
    public RelayCommand CreateVoucherForEntryCommand { get; }
    public RelayCommand ExportReportCommand { get; }

    public string Title => "Bank Reconciliation";

    public Account? SelectedBankAccount
    {
        get => _selectedBankAccount;
        set
        {
            if (SetProperty(ref _selectedBankAccount, value))
            {
                if (value != null && HasStatement)
                {
                    _ = RunReconciliationAsync();
                }
            }
        }
    }

    public string StatementFilePath
    {
        get => _statementFilePath;
        set => SetProperty(ref _statementFilePath, value);
    }

    public string StatementFileName
    {
        get => _statementFileName;
        set => SetProperty(ref _statementFileName, value);
    }

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool HasStatement
    {
        get => _hasStatement;
        set => SetProperty(ref _hasStatement, value);
    }

    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                OnPropertyChanged(nameof(IsFilterAll));
                OnPropertyChanged(nameof(IsFilterReconciled));
                OnPropertyChanged(nameof(IsFilterPartialMatch));
                OnPropertyChanged(nameof(IsFilterBankOnly));
                OnPropertyChanged(nameof(IsFilterErpOnly));
                ApplyFilter();
            }
        }
    }

    public bool IsFilterAll
    {
        get => SelectedFilter == "All";
        set { if (value) SelectedFilter = "All"; }
    }
    public bool IsFilterReconciled
    {
        get => SelectedFilter == "Reconciled";
        set { if (value) SelectedFilter = "Reconciled"; }
    }
    public bool IsFilterPartialMatch
    {
        get => SelectedFilter == "Partial Matches";
        set { if (value) SelectedFilter = "Partial Matches"; }
    }
    public bool IsFilterBankOnly
    {
        get => SelectedFilter == "Bank Only";
        set { if (value) SelectedFilter = "Bank Only"; }
    }
    public bool IsFilterErpOnly
    {
        get => SelectedFilter == "ERP Only";
        set { if (value) SelectedFilter = "ERP Only"; }
    }

    public BankReconciliationItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public decimal StatementClosingBalance
    {
        get => _statementClosingBalance;
        set => SetProperty(ref _statementClosingBalance, value);
    }

    public decimal BookClosingBalance
    {
        get => _bookClosingBalance;
        set => SetProperty(ref _bookClosingBalance, value);
    }

    public decimal UnclearedDeposits
    {
        get => _unclearedDeposits;
        set => SetProperty(ref _unclearedDeposits, value);
    }

    public decimal UnpresentedWithdrawals
    {
        get => _unpresentedWithdrawals;
        set => SetProperty(ref _unpresentedWithdrawals, value);
    }

    public decimal ReconciledBalance
    {
        get => _reconciledBalance;
        set => SetProperty(ref _reconciledBalance, value);
    }

    public decimal NetDifference
    {
        get => _netDifference;
        set => SetProperty(ref _netDifference, value);
    }

    public bool IsFullyReconciled
    {
        get => _isFullyReconciled;
        set => SetProperty(ref _isFullyReconciled, value);
    }

    public int TotalStatementCount
    {
        get => _totalStatementCount;
        set => SetProperty(ref _totalStatementCount, value);
    }

    public int ReconciledCount
    {
        get => _reconciledCount;
        set => SetProperty(ref _reconciledCount, value);
    }

    public int PartialMatchCount
    {
        get => _partialMatchCount;
        set => SetProperty(ref _partialMatchCount, value);
    }

    public int BankOnlyCount
    {
        get => _bankOnlyCount;
        set => SetProperty(ref _bankOnlyCount, value);
    }

    public int ErpOnlyCount
    {
        get => _erpOnlyCount;
        set => SetProperty(ref _erpOnlyCount, value);
    }

    public BankReconciliationViewModel(
        IAccountRepository accountRepository,
        ILedgerReportRepository ledgerReportRepository,
        IBankStatementParserService statementParser,
        Action<string, object?>? navigateModule = null)
    {
        _accountRepository = accountRepository;
        _ledgerReportRepository = ledgerReportRepository;
        _statementParser = statementParser;
        _navigateModule = navigateModule;

        BrowseStatementFileCommand = new RelayCommand(async () => await BrowseStatementFileAsync());
        RunReconciliationCommand = new AsyncRelayCommand(RunReconciliationAsync);
        AcceptPartialMatchCommand = new RelayCommand(param => AcceptPartialMatch(param as BankReconciliationItem));
        UnlinkMatchedItemCommand = new RelayCommand(param => UnlinkMatchedItem(param as BankReconciliationItem));
        CreateVoucherForEntryCommand = new RelayCommand(param => CreateVoucherForEntry(param as BankReconciliationItem));
        ExportReportCommand = new RelayCommand(ExportReport);

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        await LoadBankAccountsAsync();
    }

    public async Task LoadBankAccountsAsync()
    {
        try
        {
            var accounts = await _accountRepository.GetAllAsync();
            var banks = accounts.Where(a =>
                (!string.IsNullOrWhiteSpace(a.HeadCategory) && (a.HeadCategory.Contains("Bank", StringComparison.OrdinalIgnoreCase) || a.HeadCategory.Contains("Cash", StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrWhiteSpace(a.SubCategory) && (a.SubCategory.Contains("Bank", StringComparison.OrdinalIgnoreCase) || a.SubCategory.Contains("Cash", StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrWhiteSpace(a.BankAc) && a.BankAc != "0" && a.BankAc.Length > 2)
            ).Where(a =>
                !string.Equals(a.HeadCategory, "Creditors", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.HeadCategory, "Debtors", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.HeadCategory, "Indirect Expenses", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.HeadCategory, "Direct Expenses", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.HeadCategory, "Duties & Taxes", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.HeadCategory, "Purchases", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.HeadCategory, "Sales", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.HeadCategory, "Capital", StringComparison.OrdinalIgnoreCase)
            ).OrderBy(a => a.AccountName).ToList();

            if (banks.Count == 0)
            {
                banks = accounts.Where(a =>
                    !string.IsNullOrWhiteSpace(a.AccountName) &&
                    a.AccountName.Contains("Bank", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(a.HeadCategory, "Creditors", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(a.HeadCategory, "Debtors", StringComparison.OrdinalIgnoreCase)
                ).OrderBy(a => a.AccountName).ToList();
            }

            BankAccounts.Clear();
            foreach (var b in banks)
                BankAccounts.Add(b);

            if (SelectedBankAccount == null && BankAccounts.Count > 0)
                SelectedBankAccount = BankAccounts[0];
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading bank accounts: {ex.Message}";
        }
    }

    public async Task BrowseStatementFileAsync()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select Official Bank Statement",
            Filter = "Bank Statements (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            RestoreDirectory = true
        };

        if (ofd.ShowDialog() == true)
        {
            StatementFilePath = ofd.FileName;
            StatementFileName = Path.GetFileName(ofd.FileName);
            HasStatement = true;

            await AutoMatchBankAccountAndReconcileAsync();
        }
    }

    public async Task AutoMatchBankAccountAndReconcileAsync()
    {
        if (string.IsNullOrWhiteSpace(StatementFilePath) || !File.Exists(StatementFilePath))
            return;

        try
        {
            var parsed = await _statementParser.ParseStatementAsync(StatementFilePath);
            if (!string.IsNullOrWhiteSpace(parsed.BankName) || !string.IsNullOrWhiteSpace(parsed.AccountNumber))
            {
                var match = BankAccounts.FirstOrDefault(b =>
                    (!string.IsNullOrWhiteSpace(parsed.AccountNumber) && !string.IsNullOrWhiteSpace(b.BankAc) && b.BankAc != "0" &&
                     (b.BankAc.Contains(parsed.AccountNumber) || parsed.AccountNumber.Contains(b.BankAc))) ||
                    (!string.IsNullOrWhiteSpace(parsed.BankName) && (
                        b.AccountName.Contains(parsed.BankName, StringComparison.OrdinalIgnoreCase) ||
                        parsed.BankName.Contains(b.AccountName, StringComparison.OrdinalIgnoreCase) ||
                        (parsed.BankName.StartsWith("HDFC", StringComparison.OrdinalIgnoreCase) && b.AccountName.StartsWith("HDFC", StringComparison.OrdinalIgnoreCase)) ||
                        (parsed.BankName.StartsWith("SBI", StringComparison.OrdinalIgnoreCase) && b.AccountName.StartsWith("SBI", StringComparison.OrdinalIgnoreCase)) ||
                        (parsed.BankName.StartsWith("ICICI", StringComparison.OrdinalIgnoreCase) && b.AccountName.StartsWith("ICICI", StringComparison.OrdinalIgnoreCase)) ||
                        (parsed.BankName.StartsWith("AXIS", StringComparison.OrdinalIgnoreCase) && b.AccountName.StartsWith("AXIS", StringComparison.OrdinalIgnoreCase)) ||
                        (parsed.BankName.StartsWith("KOTAK", StringComparison.OrdinalIgnoreCase) && b.AccountName.StartsWith("KOTAK", StringComparison.OrdinalIgnoreCase))
                    ))
                );

                if (match != null)
                    SelectedBankAccount = match;
            }

            if (parsed.FromDate.HasValue) FromDate = parsed.FromDate.Value;
            if (parsed.ToDate.HasValue) ToDate = parsed.ToDate.Value;

            await RunReconciliationAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Statement Parsing Error]: {ex}");
            StatusMessage = $"Error parsing statement: {ex.Message}";
            if (System.Windows.Application.Current != null)
            {
                MessageBox.Show($"Error reading statement file:\n\n{ex.Message}", "Bank Statement Parsing Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    public async Task RunReconciliationAsync()
    {
        if (SelectedBankAccount == null)
        {
            StatusMessage = "Please select a Bank Account.";
            return;
        }

        if (string.IsNullOrWhiteSpace(StatementFilePath) || !File.Exists(StatementFilePath))
        {
            StatusMessage = "Please load a Bank Statement file (.xlsx, .xls, .csv).";
            return;
        }

        IsLoading = true;
        StatusMessage = "Parsing bank statement and reconciling against ERP bank ledger...";

        try
        {
            // 1. Parse statement
            var statement = await _statementParser.ParseStatementAsync(StatementFilePath);
            if (statement.Transactions.Count == 0)
            {
                StatusMessage = "No transaction rows could be parsed from the statement file. Check format.";
                MessageBox.Show("No transaction rows could be parsed from the statement file.\n\nPlease check if the file format or header row is supported.", "Statement Parsing", MessageBoxButton.OK, MessageBoxImage.Information);
                IsLoading = false;
                return;
            }

            // Sync statement date window with pickers
            if (statement.FromDate.HasValue)
                FromDate = statement.FromDate.Value;
            if (statement.ToDate.HasValue)
                ToDate = statement.ToDate.Value;

            // 2. Fetch ERP Ledger for Bank
            var filter = new LedgerReportFilter
            {
                AccountName = SelectedBankAccount.AccountName,
                FromDate = FromDate,
                ToDate = ToDate,
                PrintOption = LedgerPrintOption.Running
            };

            var erpRows = (await _ledgerReportRepository.GetLedgerInRupeesAsync(filter)).ToList();
            var summary = await _ledgerReportRepository.GetAccountSummaryAsync(SelectedBankAccount.AccountName, FromDate, ToDate);

            // 3. Multi-Pass Match Engine
            // Bank perspective: Deposit = Credit (+), Withdrawal = Debit (-)
            // ERP perspective: Deposit = Debit (+), Payment = Credit (-)
            var matchedStatementIndices = new HashSet<int>();
            var matchedErpIndices = new HashSet<int>();
            var items = new List<BankReconciliationItem>();

            // Pass 1: Exact Amount & Reference/Cheque number match (High Confidence Reconciled)
            for (int sIdx = 0; sIdx < statement.Transactions.Count; sIdx++)
            {
                var stmt = statement.Transactions[sIdx];
                if (string.IsNullOrWhiteSpace(stmt.ReferenceNo)) continue;

                var cleanRef = stmt.ReferenceNo.Trim();
                for (int eIdx = 0; eIdx < erpRows.Count; eIdx++)
                {
                    if (matchedErpIndices.Contains(eIdx)) continue;
                    var erp = erpRows[eIdx];

                    var dayDiff = Math.Abs((stmt.Date - erp.VouDate).TotalDays);
                    if (dayDiff > 60) continue;

                    bool refMatches = (!string.IsNullOrWhiteSpace(erp.ChequeNo) && erp.ChequeNo.Trim().Equals(cleanRef, StringComparison.OrdinalIgnoreCase)) ||
                                      (!string.IsNullOrWhiteSpace(erp.Narration) && (erp.Narration.Contains(cleanRef, StringComparison.OrdinalIgnoreCase) || cleanRef.Contains(erp.Narration, StringComparison.OrdinalIgnoreCase))) ||
                                      (!string.IsNullOrWhiteSpace(erp.Head) && erp.Head.Contains(cleanRef, StringComparison.OrdinalIgnoreCase));

                    if (!refMatches) continue;

                    // Match Statement Debit (Withdrawal) with ERP Credit (Payment)
                    if (stmt.Debit > 0 && Math.Abs(stmt.Debit - erp.Credit) < 0.01m)
                    {
                        matchedStatementIndices.Add(sIdx);
                        matchedErpIndices.Add(eIdx);
                        items.Add(new BankReconciliationItem
                        {
                            Status = BankReconciliationStatus.Reconciled,
                            TransactionDate = stmt.Date,
                            Description = stmt.Description,
                            ReferenceNo = stmt.ReferenceNo,
                            StatementDebit = stmt.Debit,
                            ErpCredit = erp.Credit,
                            ErpVouNo = erp.VouNo,
                            ErpVouType = erp.VouType,
                            ErpParticulars = erp.Head,
                            MatchScore = 1.0,
                            MatchedNotes = $"Matched by Chq/Ref #{cleanRef} (Payment Rs.{stmt.Debit:N2})"
                        });
                        break;
                    }

                    // Match Statement Credit (Deposit) with ERP Debit (Receipt)
                    if (stmt.Credit > 0 && Math.Abs(stmt.Credit - erp.Debit) < 0.01m)
                    {
                        matchedStatementIndices.Add(sIdx);
                        matchedErpIndices.Add(eIdx);
                        items.Add(new BankReconciliationItem
                        {
                            Status = BankReconciliationStatus.Reconciled,
                            TransactionDate = stmt.Date,
                            Description = stmt.Description,
                            ReferenceNo = stmt.ReferenceNo,
                            StatementCredit = stmt.Credit,
                            ErpDebit = erp.Debit,
                            ErpVouNo = erp.VouNo,
                            ErpVouType = erp.VouType,
                            ErpParticulars = erp.Head,
                            MatchScore = 1.0,
                            MatchedNotes = $"Matched by Chq/Ref #{cleanRef} (Receipt Rs.{stmt.Credit:N2})"
                        });
                        break;
                    }
                }
            }

            // Pass 2: Exact Amount + Date Proximity (within +-4 days) + Verified Party Fuzzy Match
            for (int sIdx = 0; sIdx < statement.Transactions.Count; sIdx++)
            {
                if (matchedStatementIndices.Contains(sIdx)) continue;
                var stmt = statement.Transactions[sIdx];

                for (int eIdx = 0; eIdx < erpRows.Count; eIdx++)
                {
                    if (matchedErpIndices.Contains(eIdx)) continue;
                    var erp = erpRows[eIdx];

                    var dayDiff = Math.Abs((stmt.Date - erp.VouDate).TotalDays);
                    if (dayDiff > 4) continue;

                    var fuzzy = FuzzyTextMatcher.MatchParty(stmt.Description, erp.Head);
                    if (!fuzzy.IsMatch && !string.IsNullOrWhiteSpace(erp.Narration))
                        fuzzy = FuzzyTextMatcher.MatchParty(stmt.Description, erp.Narration);

                    if (!fuzzy.IsMatch || fuzzy.Score < 0.50) continue;

                    if (stmt.Debit > 0 && Math.Abs(stmt.Debit - erp.Credit) < 0.01m)
                    {
                        matchedStatementIndices.Add(sIdx);
                        matchedErpIndices.Add(eIdx);
                        items.Add(new BankReconciliationItem
                        {
                            Status = BankReconciliationStatus.Reconciled,
                            TransactionDate = stmt.Date,
                            Description = stmt.Description,
                            ReferenceNo = !string.IsNullOrWhiteSpace(stmt.ReferenceNo) ? stmt.ReferenceNo : erp.ChequeNo,
                            StatementDebit = stmt.Debit,
                            ErpCredit = erp.Credit,
                            ErpVouNo = erp.VouNo,
                            ErpVouType = erp.VouType,
                            ErpParticulars = erp.Head,
                            MatchScore = fuzzy.Score,
                            MatchedNotes = $"Matched by Amount, Date (+-{(int)dayDiff}d) & Party ('{erp.Head}')"
                        });
                        break;
                    }

                    if (stmt.Credit > 0 && Math.Abs(stmt.Credit - erp.Debit) < 0.01m)
                    {
                        matchedStatementIndices.Add(sIdx);
                        matchedErpIndices.Add(eIdx);
                        items.Add(new BankReconciliationItem
                        {
                            Status = BankReconciliationStatus.Reconciled,
                            TransactionDate = stmt.Date,
                            Description = stmt.Description,
                            ReferenceNo = !string.IsNullOrWhiteSpace(stmt.ReferenceNo) ? stmt.ReferenceNo : erp.ChequeNo,
                            StatementCredit = stmt.Credit,
                            ErpDebit = erp.Debit,
                            ErpVouNo = erp.VouNo,
                            ErpVouType = erp.VouType,
                            ErpParticulars = erp.Head,
                            MatchScore = fuzzy.Score,
                            MatchedNotes = $"Matched by Amount, Date (+-{(int)dayDiff}d) & Party ('{erp.Head}')"
                        });
                        break;
                    }
                }
            }

            // Pass 3: PARTIAL MATCH - Amount Match + Fuzzy Party Match (within +-30 days)
            for (int sIdx = 0; sIdx < statement.Transactions.Count; sIdx++)
            {
                if (matchedStatementIndices.Contains(sIdx)) continue;
                var stmt = statement.Transactions[sIdx];

                for (int eIdx = 0; eIdx < erpRows.Count; eIdx++)
                {
                    if (matchedErpIndices.Contains(eIdx)) continue;
                    var erp = erpRows[eIdx];

                    var dayDiff = Math.Abs((stmt.Date - erp.VouDate).TotalDays);
                    if (dayDiff > 30) continue;

                    var fuzzy = FuzzyTextMatcher.MatchParty(stmt.Description, erp.Head);
                    if (!fuzzy.IsMatch && !string.IsNullOrWhiteSpace(erp.Narration))
                        fuzzy = FuzzyTextMatcher.MatchParty(stmt.Description, erp.Narration);

                    if (!fuzzy.IsMatch) continue;

                    if (stmt.Debit > 0 && Math.Abs(stmt.Debit - erp.Credit) < 0.01m)
                    {
                        matchedStatementIndices.Add(sIdx);
                        matchedErpIndices.Add(eIdx);
                        items.Add(new BankReconciliationItem
                        {
                            Status = BankReconciliationStatus.PartialMatch,
                            TransactionDate = stmt.Date,
                            Description = stmt.Description,
                            ReferenceNo = !string.IsNullOrWhiteSpace(stmt.ReferenceNo) ? stmt.ReferenceNo : erp.ChequeNo,
                            StatementDebit = stmt.Debit,
                            ErpCredit = erp.Credit,
                            ErpVouNo = erp.VouNo,
                            ErpVouType = erp.VouType,
                            ErpParticulars = erp.Head,
                            MatchScore = fuzzy.Score,
                            MatchedNotes = $"⚡ Probable Match: {fuzzy.Reason} (+-{(int)dayDiff}d delay). Needs review."
                        });
                        break;
                    }

                    if (stmt.Credit > 0 && Math.Abs(stmt.Credit - erp.Debit) < 0.01m)
                    {
                        matchedStatementIndices.Add(sIdx);
                        matchedErpIndices.Add(eIdx);
                        items.Add(new BankReconciliationItem
                        {
                            Status = BankReconciliationStatus.PartialMatch,
                            TransactionDate = stmt.Date,
                            Description = stmt.Description,
                            ReferenceNo = !string.IsNullOrWhiteSpace(stmt.ReferenceNo) ? stmt.ReferenceNo : erp.ChequeNo,
                            StatementCredit = stmt.Credit,
                            ErpDebit = erp.Debit,
                            ErpVouNo = erp.VouNo,
                            ErpVouType = erp.VouType,
                            ErpParticulars = erp.Head,
                            MatchScore = fuzzy.Score,
                            MatchedNotes = $"⚡ Probable Match: {fuzzy.Reason} (+-{(int)dayDiff}d delay). Needs review."
                        });
                        break;
                    }
                }
            }

            // Pass 4: PARTIAL MATCH - Amount & Date Match (within +-3 days delay), Party unverified
            for (int sIdx = 0; sIdx < statement.Transactions.Count; sIdx++)
            {
                if (matchedStatementIndices.Contains(sIdx)) continue;
                var stmt = statement.Transactions[sIdx];

                for (int eIdx = 0; eIdx < erpRows.Count; eIdx++)
                {
                    if (matchedErpIndices.Contains(eIdx)) continue;
                    var erp = erpRows[eIdx];

                    var dayDiff = Math.Abs((stmt.Date - erp.VouDate).TotalDays);
                    if (dayDiff > 3) continue;

                    if (stmt.Debit > 0 && Math.Abs(stmt.Debit - erp.Credit) < 0.01m)
                    {
                        matchedStatementIndices.Add(sIdx);
                        matchedErpIndices.Add(eIdx);
                        items.Add(new BankReconciliationItem
                        {
                            Status = BankReconciliationStatus.PartialMatch,
                            TransactionDate = stmt.Date,
                            Description = stmt.Description,
                            ReferenceNo = !string.IsNullOrWhiteSpace(stmt.ReferenceNo) ? stmt.ReferenceNo : erp.ChequeNo,
                            StatementDebit = stmt.Debit,
                            ErpCredit = erp.Credit,
                            ErpVouNo = erp.VouNo,
                            ErpVouType = erp.VouType,
                            ErpParticulars = erp.Head,
                            MatchScore = 0.50,
                            MatchedNotes = $"⚡ Probable Match: Amount & Date match (+-{(int)dayDiff}d delay), party unconfirmed ('{erp.Head}')."
                        });
                        break;
                    }

                    if (stmt.Credit > 0 && Math.Abs(stmt.Credit - erp.Debit) < 0.01m)
                    {
                        matchedStatementIndices.Add(sIdx);
                        matchedErpIndices.Add(eIdx);
                        items.Add(new BankReconciliationItem
                        {
                            Status = BankReconciliationStatus.PartialMatch,
                            TransactionDate = stmt.Date,
                            Description = stmt.Description,
                            ReferenceNo = !string.IsNullOrWhiteSpace(stmt.ReferenceNo) ? stmt.ReferenceNo : erp.ChequeNo,
                            StatementCredit = stmt.Credit,
                            ErpDebit = erp.Debit,
                            ErpVouNo = erp.VouNo,
                            ErpVouType = erp.VouType,
                            ErpParticulars = erp.Head,
                            MatchScore = 0.50,
                            MatchedNotes = $"⚡ Probable Match: Amount & Date match (+-{(int)dayDiff}d delay), party unconfirmed ('{erp.Head}')."
                        });
                        break;
                    }
                }
            }

            // Unmatched Statement items -> Bank Only
            for (int sIdx = 0; sIdx < statement.Transactions.Count; sIdx++)
            {
                if (matchedStatementIndices.Contains(sIdx)) continue;
                var stmt = statement.Transactions[sIdx];
                items.Add(new BankReconciliationItem
                {
                    Status = BankReconciliationStatus.BankOnly,
                    TransactionDate = stmt.Date,
                    Description = stmt.Description,
                    ReferenceNo = stmt.ReferenceNo,
                    StatementDebit = stmt.Debit,
                    StatementCredit = stmt.Credit,
                    MatchedNotes = stmt.Debit > 0 ? "Bank Debit not found in ERP books (Auto Voucher available)" : "Bank Credit not found in ERP books (Auto Voucher available)"
                });
            }

            // Unmatched ERP ledger rows -> ERP Only (Uncleared / Unpresented)
            for (int eIdx = 0; eIdx < erpRows.Count; eIdx++)
            {
                if (matchedErpIndices.Contains(eIdx)) continue;
                var erp = erpRows[eIdx];

                if (erp.Head != null && (erp.Head.StartsWith("Opening", StringComparison.OrdinalIgnoreCase) || erp.Head.StartsWith("Closing", StringComparison.OrdinalIgnoreCase)))
                    continue;

                items.Add(new BankReconciliationItem
                {
                    Status = BankReconciliationStatus.ErpOnly,
                    TransactionDate = erp.VouDate,
                    Description = erp.Head ?? string.Empty,
                    ReferenceNo = !string.IsNullOrWhiteSpace(erp.ChequeNo) ? erp.ChequeNo : (erp.Narration ?? string.Empty),
                    ErpDebit = erp.Debit,
                    ErpCredit = erp.Credit,
                    ErpVouNo = erp.VouNo,
                    ErpVouType = erp.VouType,
                    ErpParticulars = erp.Head ?? string.Empty,
                    MatchedNotes = erp.Debit > 0 ? "Uncleared Deposit (In ERP, not yet credited by bank)" : "Unpresented Cheque (In ERP, not yet debited by bank)"
                });
            }

            var sorted = items.OrderByDescending(i => i.TransactionDate).ToList();

            AllItems.Clear();
            foreach (var item in sorted)
                AllItems.Add(item);

            // 4. Calculate Reconciliation Summary
            StatementClosingBalance = statement.ClosingBalance;
            BookClosingBalance = summary.ClosingType.Equals("Cr", StringComparison.OrdinalIgnoreCase)
                ? -summary.ClosingBalance
                : summary.ClosingBalance;

            TotalStatementCount = statement.Transactions.Count;
            ReconciledCount = items.Count(i => i.IsReconciled);
            PartialMatchCount = items.Count(i => i.IsPartialMatch);
            BankOnlyCount = items.Count(i => i.IsBankOnly);
            ErpOnlyCount = items.Count(i => i.IsErpOnly);

            // Compute balances directly from items to avoid collection concurrency
            UnclearedDeposits = items.Where(i => i.IsErpOnly && i.ErpDebit > 0).Sum(i => i.ErpDebit);
            UnpresentedWithdrawals = items.Where(i => i.IsErpOnly && i.ErpCredit > 0).Sum(i => i.ErpCredit);
            ReconciledBalance = StatementClosingBalance + UnclearedDeposits - UnpresentedWithdrawals;
            NetDifference = Math.Round(ReconciledBalance - BookClosingBalance, 2);
            IsFullyReconciled = Math.Abs(NetDifference) < 0.01m;

            ApplyFilter();

            StatusMessage = IsFullyReconciled
                ? $"✓ Bank Reconciliation Complete! Balances fully reconciled for {SelectedBankAccount.AccountName}."
                : $"Reconciliation computed: {ReconciledCount} reconciled, {PartialMatchCount} partial matches, {BankOnlyCount} in bank only, {ErpOnlyCount} in ERP only. Difference: Rs.{NetDifference:N2}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Reconciliation Error StackTrace]: {ex}");
            StatusMessage = $"Reconciliation failed: {ex.Message}";
            if (System.Windows.Application.Current != null)
            {
                MessageBox.Show($"Reconciliation failed:\n\n{ex.Message}", "Bank Reconciliation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RecalculateBalances()
    {
        UnclearedDeposits = AllItems.Where(i => i != null && i.IsErpOnly && i.ErpDebit > 0).Sum(i => i.ErpDebit);
        UnpresentedWithdrawals = AllItems.Where(i => i != null && i.IsErpOnly && i.ErpCredit > 0).Sum(i => i.ErpCredit);

        ReconciledBalance = StatementClosingBalance + UnclearedDeposits - UnpresentedWithdrawals;
        NetDifference = Math.Round(ReconciledBalance - BookClosingBalance, 2);
        IsFullyReconciled = Math.Abs(NetDifference) < 0.01m;
    }

    private void ApplyFilter()
    {
        var query = AllItems.Where(i => i != null);
        if (SelectedFilter == "Reconciled")
            query = query.Where(i => i.IsReconciled);
        else if (SelectedFilter == "Partial Matches")
            query = query.Where(i => i.IsPartialMatch);
        else if (SelectedFilter == "Bank Only")
            query = query.Where(i => i.IsBankOnly);
        else if (SelectedFilter == "ERP Only")
            query = query.Where(i => i.IsErpOnly);

        FilteredItems.Clear();
        foreach (var item in query)
            FilteredItems.Add(item);
    }

    public void AcceptPartialMatch(BankReconciliationItem? item)
    {
        var target = item ?? SelectedItem;
        if (target == null || !target.IsPartialMatch)
            return;

        target.Status = BankReconciliationStatus.Reconciled;
        target.MatchedNotes = $"✓ Confirmed: {target.MatchedNotes.Replace("⚡ Probable Match: ", "")}";

        ReconciledCount = AllItems.Count(i => i.IsReconciled);
        PartialMatchCount = AllItems.Count(i => i.IsPartialMatch);
        BankOnlyCount = AllItems.Count(i => i.IsBankOnly);
        ErpOnlyCount = AllItems.Count(i => i.IsErpOnly);

        RecalculateBalances();
        ApplyFilter();
        StatusMessage = $"Confirmed match for voucher {target.ErpVouNo} ({target.Description}).";
    }

    public void UnlinkMatchedItem(BankReconciliationItem? item)
    {
        var target = item ?? SelectedItem;
        if (target == null || (!target.IsReconciled && !target.IsPartialMatch))
            return;

        AllItems.Remove(target);

        // Bank Only item
        var bankItem = new BankReconciliationItem
        {
            Status = BankReconciliationStatus.BankOnly,
            TransactionDate = target.TransactionDate,
            Description = target.Description,
            ReferenceNo = target.ReferenceNo,
            StatementDebit = target.StatementDebit,
            StatementCredit = target.StatementCredit,
            MatchedNotes = "Unlinked from ERP voucher by user"
        };
        AllItems.Add(bankItem);

        // ERP Only item
        if (!string.IsNullOrWhiteSpace(target.ErpVouNo))
        {
            var erpItem = new BankReconciliationItem
            {
                Status = BankReconciliationStatus.ErpOnly,
                TransactionDate = target.TransactionDate,
                Description = !string.IsNullOrWhiteSpace(target.ErpParticulars) ? target.ErpParticulars : target.Description,
                ReferenceNo = target.ReferenceNo,
                ErpDebit = target.ErpDebit,
                ErpCredit = target.ErpCredit,
                ErpVouNo = target.ErpVouNo,
                ErpVouType = target.ErpVouType,
                ErpParticulars = target.ErpParticulars,
                MatchedNotes = "Unlinked from bank statement by user"
            };
            AllItems.Add(erpItem);
        }

        ReconciledCount = AllItems.Count(i => i.IsReconciled);
        PartialMatchCount = AllItems.Count(i => i.IsPartialMatch);
        BankOnlyCount = AllItems.Count(i => i.IsBankOnly);
        ErpOnlyCount = AllItems.Count(i => i.IsErpOnly);

        RecalculateBalances();
        ApplyFilter();
        StatusMessage = $"Unlinked voucher {target.ErpVouNo}.";
    }

    public void CreateVoucherForEntry(BankReconciliationItem? item)
    {
        var target = item ?? SelectedItem;
        if (target == null || !target.IsBankOnly || SelectedBankAccount == null)
            return;

        if (target.StatementDebit > 0)
        {
            _navigateModule?.Invoke("Payments", target.StatementDebit);
        }
        else if (target.StatementCredit > 0)
        {
            _navigateModule?.Invoke("Receipts", target.StatementCredit);
        }
    }

    public void ExportReport()
    {
        if (AllItems.Count == 0)
        {
            MessageBox.Show("No reconciliation items to export.", "BRS Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sfd = new SaveFileDialog
        {
            FileName = $"BRS_{SelectedBankAccount?.AccountName}_{DateTime.Now:yyyyMMdd}.csv",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                using var sw = new StreamWriter(sfd.FileName);
                sw.WriteLine($"Bank Reconciliation Statement: {SelectedBankAccount?.AccountName}");
                sw.WriteLine($"Period: {FromDate:dd/MM/yyyy} to {ToDate:dd/MM/yyyy}");
                sw.WriteLine($"Bank Statement Closing Balance,{StatementClosingBalance}");
                sw.WriteLine($"Add: Uncleared Deposits (+),{UnclearedDeposits}");
                sw.WriteLine($"Less: Unpresented Payments (-),{UnpresentedWithdrawals}");
                sw.WriteLine($"Reconciled Book Balance,{ReconciledBalance}");
                sw.WriteLine($"ERP Ledger Balance,{BookClosingBalance}");
                sw.WriteLine($"Net Difference,{NetDifference}");
                sw.WriteLine();
                sw.WriteLine("Status,Date,Description,Ref No,Bank Debit,Bank Credit,ERP Debit,ERP Credit,ERP Vou No,ERP Vou Type,Particulars,Notes");

                foreach (var i in AllItems)
                {
                    sw.WriteLine($"\"{i.StatusBadgeText}\",{i.TransactionDate:dd/MM/yyyy},\"{i.Description.Replace("\"", "\"\"")}\",\"{i.ReferenceNo}\",{i.StatementDebit},{i.StatementCredit},{i.ErpDebit},{i.ErpCredit},\"{i.ErpVouNo}\",\"{i.ErpVouType}\",\"{i.ErpParticulars}\",\"{i.MatchedNotes}\"");
                }

                StatusMessage = $"BRS report saved to {sfd.FileName}";
                MessageBox.Show($"Bank Reconciliation Statement exported successfully to {sfd.FileName}", "BRS Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export BRS report: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
