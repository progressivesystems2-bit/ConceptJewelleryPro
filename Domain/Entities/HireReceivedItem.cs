using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class HireReceivedItem : INotifyPropertyChanged
{
    private string _recNo = string.Empty;
    private int _nos;
    private DateTime _recDate = DateTime.Today;
    private string _partyName = string.Empty;
    private string _itemType = string.Empty;
    private decimal _weightRecd;
    private string _note = string.Empty;
    private string _entryUser = string.Empty;
    private DateTime _entryDate = DateTime.Today;
    private DateTime _entryTime = DateTime.Now;
    private string _computerName = string.Empty;
    private string _status = "Saved";

    public string RecNo
    {
        get => _recNo;
        set => SetField(ref _recNo, value);
    }

    public int Nos
    {
        get => _nos;
        set => SetField(ref _nos, value);
    }

    public DateTime RecDate
    {
        get => _recDate;
        set => SetField(ref _recDate, value);
    }

    public string PartyName
    {
        get => _partyName;
        set => SetField(ref _partyName, value);
    }

    public string ItemType
    {
        get => _itemType;
        set => SetField(ref _itemType, value);
    }

    public decimal WeightRecd
    {
        get => _weightRecd;
        set => SetField(ref _weightRecd, value);
    }

    public string Note
    {
        get => _note;
        set => SetField(ref _note, value);
    }

    public string EntryUser
    {
        get => _entryUser;
        set => SetField(ref _entryUser, value);
    }

    public DateTime EntryDate
    {
        get => _entryDate;
        set => SetField(ref _entryDate, value);
    }

    public DateTime EntryTime
    {
        get => _entryTime;
        set => SetField(ref _entryTime, value);
    }

    public string ComputerName
    {
        get => _computerName;
        set => SetField(ref _computerName, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
