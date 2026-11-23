using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class MetalIssuedItem : INotifyPropertyChanged
{
    private string _issueNo = string.Empty;
    private int _nos;
    private DateTime _issueDate = DateTime.Today;
    private string _artisianName = string.Empty;
    private string _itemType = string.Empty;
    private decimal _weightIssued;
    private string _purity = string.Empty;
    private string _note = string.Empty;
    private string _entryUser = string.Empty;
    private DateTime _entryDate = DateTime.Today;
    private DateTime _entryTime = DateTime.Now;
    private string _computerName = string.Empty;
    private string _status = "Saved";

    public string IssueNo
    {
        get => _issueNo;
        set => SetField(ref _issueNo, value);
    }

    public int Nos
    {
        get => _nos;
        set => SetField(ref _nos, value);
    }

    public DateTime IssueDate
    {
        get => _issueDate;
        set => SetField(ref _issueDate, value);
    }

    public string ArtisianName
    {
        get => _artisianName;
        set => SetField(ref _artisianName, value);
    }

    public string ItemType
    {
        get => _itemType;
        set => SetField(ref _itemType, value);
    }

    public decimal WeightIssued
    {
        get => _weightIssued;
        set => SetField(ref _weightIssued, value);
    }

    public string Purity
    {
        get => _purity;
        set => SetField(ref _purity, value);
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
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
