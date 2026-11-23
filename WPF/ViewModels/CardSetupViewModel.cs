using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.WPF.ViewModels;

public class CardSetupViewModel : ViewModelBase
{
    private readonly ICardSetupRepository _cardSetupRepository;
    private readonly IAccountRepository _accountRepository;

    private CardSetupItemModel? _selectedItem;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public CardSetupItemModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string Title { get; set; } = "Card Setup";

    public ObservableCollection<CardSetupItemModel> Items { get; } = [];
    public ObservableCollection<string> AvailableBankAccounts { get; } = [];

    public RelayCommand AddCardCommand { get; }
    public RelayCommand DeleteCardCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ReloadCommand { get; }

    public CardSetupViewModel(ICardSetupRepository cardSetupRepository, IAccountRepository accountRepository)
    {
        _cardSetupRepository = cardSetupRepository;
        _accountRepository = accountRepository;

        AddCardCommand = new RelayCommand(AddCard);
        DeleteCardCommand = new RelayCommand(DeleteCard);
        MoveUpCommand = new RelayCommand(MoveUp);
        MoveDownCommand = new RelayCommand(MoveDown);
        SaveCommand = new AsyncRelayCommand(async () => await SaveAsync());
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
    }

    public async Task InitializeAsync()
    {
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        IsBusy = true;
        try
        {
            Items.Clear();
            AvailableBankAccounts.Clear();

            var accounts = await _accountRepository.GetAllAsync();
            foreach (var acc in accounts
                .Where(a => !string.Equals(a.Status, "Deleted", StringComparison.OrdinalIgnoreCase))
                .Where(a => (a.HeadCategory?.Contains("Bank", StringComparison.OrdinalIgnoreCase) == true)
                         || (a.HeadCategory?.Contains("Card", StringComparison.OrdinalIgnoreCase) == true)
                         || (a.HeadCategory?.Contains("OD", StringComparison.OrdinalIgnoreCase) == true)
                         || (a.AccountName.Contains("Card", StringComparison.OrdinalIgnoreCase))
                         || (a.AccountName.Contains("UPI", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(a => a.AccountName))
            {
                if (!string.IsNullOrWhiteSpace(acc.AccountName) && !AvailableBankAccounts.Contains(acc.AccountName))
                    AvailableBankAccounts.Add(acc.AccountName);
            }

            var cards = await _cardSetupRepository.GetAllAsync();
            int defaultOrder = 1;
            foreach (var card in cards.OrderBy(c => c.Order))
            {
                var model = CardSetupItemModel.FromEntity(card);
                if (model.Order <= 0) model.Order = defaultOrder;
                Items.Add(model);
                defaultOrder++;

                if (!string.IsNullOrWhiteSpace(model.CardBank) && !AvailableBankAccounts.Contains(model.CardBank))
                    AvailableBankAccounts.Add(model.CardBank);
            }

            RenumerateOrders();
            SelectedItem = Items.FirstOrDefault();
            StatusMessage = $"Loaded {Items.Count} cards.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddCard()
    {
        var existingNames = new HashSet<string>(Items.Select(x => x.CardBank.Trim()), StringComparer.OrdinalIgnoreCase);
        var nextAvailable = AvailableBankAccounts.FirstOrDefault(a => !existingNames.Contains(a)) ?? string.Empty;

        var newItem = new CardSetupItemModel
        {
            Order = Items.Count + 1,
            CardBank = nextAvailable,
            CardComm = 0m,
            Sbi = 0m
        };
        Items.Add(newItem);
        SelectedItem = newItem;
        StatusMessage = "New card row added.";
    }

    private void DeleteCard()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "Please select a card to delete.";
            return;
        }

        var index = Items.IndexOf(SelectedItem);
        Items.Remove(SelectedItem);
        RenumerateOrders();
        if (Items.Count > 0)
        {
            SelectedItem = Items[Math.Clamp(index, 0, Items.Count - 1)];
        }
        StatusMessage = "Card removed.";
    }

    private void MoveUp()
    {
        if (SelectedItem == null) return;
        var index = Items.IndexOf(SelectedItem);
        if (index <= 0) return;

        var item = SelectedItem;
        Items.RemoveAt(index);
        Items.Insert(index - 1, item);
        RenumerateOrders();
        SelectedItem = item;
    }

    private void MoveDown()
    {
        if (SelectedItem == null) return;
        var index = Items.IndexOf(SelectedItem);
        if (index < 0 || index >= Items.Count - 1) return;

        var item = SelectedItem;
        Items.RemoveAt(index);
        Items.Insert(index + 1, item);
        RenumerateOrders();
        SelectedItem = item;
    }

    public async Task<bool> SaveAsync()
    {
        foreach (var item in Items)
        {
            if (string.IsNullOrWhiteSpace(item.CardBank))
            {
                StatusMessage = "Card Bank Name cannot be empty.";
                return false;
            }
        }

        var duplicates = Items
            .GroupBy(x => x.CardBank.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            StatusMessage = $"Duplicate card names found: {string.Join(", ", duplicates)}";
            return false;
        }

        IsBusy = true;
        try
        {
            RenumerateOrders();
            var entities = Items.Select(x => x.ToEntity()).ToList();
            await _cardSetupRepository.SaveAllAsync(entities);
            StatusMessage = "Card setup saved successfully.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RenumerateOrders()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].Order = i + 1;
        }
    }
}
