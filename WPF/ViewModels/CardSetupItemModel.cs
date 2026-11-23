using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.WPF.ViewModels;

public class CardSetupItemModel : ViewModelBase
{
    private int _order;
    private string _cardBank = string.Empty;
    private decimal _cardComm;
    private decimal _sbi;

    public int Order
    {
        get => _order;
        set => SetProperty(ref _order, value);
    }

    public string CardBank
    {
        get => _cardBank;
        set => SetProperty(ref _cardBank, value);
    }

    public decimal CardComm
    {
        get => _cardComm;
        set => SetProperty(ref _cardComm, value);
    }

    public decimal Sbi
    {
        get => _sbi;
        set => SetProperty(ref _sbi, value);
    }

    public CardSetupItem ToEntity() => new()
    {
        CardBank = CardBank?.Trim() ?? string.Empty,
        CardComm = CardComm,
        Sbi = Sbi,
        Order = Order
    };

    public static CardSetupItemModel FromEntity(CardSetupItem entity) => new()
    {
        CardBank = entity.CardBank ?? string.Empty,
        CardComm = entity.CardComm,
        Sbi = entity.Sbi,
        Order = entity.Order
    };
}
