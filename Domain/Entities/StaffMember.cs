using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class StaffMember : INotifyPropertyChanged
{
    private string _staff = string.Empty;
    private string _status = "Active";
    private string _entryUser = string.Empty;
    private DateTime _entryDate = DateTime.Today;
    private DateTime _entryTime = DateTime.Now;
    private string _computerName = string.Empty;

    public string Staff
    {
        get => _staff;
        set => SetField(ref _staff, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
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
