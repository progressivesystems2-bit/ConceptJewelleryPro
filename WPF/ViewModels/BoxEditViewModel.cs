using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class BoxEditViewModel : ViewModelBase
{
    private readonly IBoxMasterRepository _repository;
    private readonly string _currentUser;
    private readonly string? _originalBoxNo;

    private string _boxNo = string.Empty;
    private string _boxName = string.Empty;
    private string _rack = string.Empty;
    private int _numberOfBoxes = 1;
    private decimal _boxWt;
    private int _serial;
    private string _status = "Active";
    private string _remarks = string.Empty;
    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private string _errorMessage = string.Empty;

    public event EventHandler<bool?>? RequestClose;

    public BoxEditViewModel(
        IBoxMasterRepository repository,
        BoxMaster? existingBox = null,
        string currentUser = "admin",
        IReadOnlyList<string>? preloadedRacks = null)
    {
        _repository = repository;
        _currentUser = currentUser;

        AvailableRacks = new ObservableCollection<string>();
        if (preloadedRacks is not null)
        {
            foreach (var r in preloadedRacks)
            {
                if (!string.IsNullOrWhiteSpace(r) && !AvailableRacks.Contains(r))
                    AvailableRacks.Add(r);
            }
        }

        if (existingBox is not null)
        {
            _originalBoxNo = existingBox.BoxNo;
            _boxNo = existingBox.BoxNo;
            _boxName = existingBox.BoxName;
            _rack = existingBox.Rack;
            _numberOfBoxes = existingBox.NumberOfBoxes > 0 ? existingBox.NumberOfBoxes : 1;
            _boxWt = existingBox.BoxWt;
            _serial = existingBox.Serial;
            _status = string.IsNullOrWhiteSpace(existingBox.Status) ? "Active" : existingBox.Status;
            _remarks = existingBox.Remarks;
            Title = $"Modify Box - {existingBox.BoxNo}";
            IsNew = false;
        }
        else
        {
            Title = "Add New Box";
            IsNew = true;
            _status = "Active";
            _numberOfBoxes = 1;
            _boxWt = 0m;
            _ = LoadNextSerialAsync();
        }

        SaveCommand = new RelayCommand(async () => await SaveAsync());
        CancelCommand = new RelayCommand(Cancel);

        _ = LoadRacksAsync();
    }

    public string Title { get; }

    public bool IsNew { get; }

    public ObservableCollection<string> AvailableRacks { get; }

    public ObservableCollection<string> StatusOptions { get; } = new() { "Active", "Inactive" };

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public string BoxNo
    {
        get => _boxNo;
        set
        {
            if (SetProperty(ref _boxNo, value))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    public string BoxName
    {
        get => _boxName;
        set => SetProperty(ref _boxName, value);
    }

    public string Rack
    {
        get => _rack;
        set => SetProperty(ref _rack, value);
    }

    public int NumberOfBoxes
    {
        get => _numberOfBoxes;
        set => SetProperty(ref _numberOfBoxes, value);
    }

    public decimal BoxWt
    {
        get => _boxWt;
        set => SetProperty(ref _boxWt, value);
    }

    public int Serial
    {
        get => _serial;
        set => SetProperty(ref _serial, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Remarks
    {
        get => _remarks;
        set => SetProperty(ref _remarks, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? ToastMessage
    {
        get => _toastMessage;
        private set => SetProperty(ref _toastMessage, value);
    }

    public ToastType ToastType
    {
        get => _toastType;
        private set => SetProperty(ref _toastType, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public BoxMaster? ResultBox { get; private set; }

    private async Task LoadNextSerialAsync()
    {
        try
        {
            var nextSerial = await _repository.GetNextSerialAsync();
            if (Serial <= 0)
            {
                Serial = nextSerial;
            }
        }
        catch { }
    }

    private async Task LoadRacksAsync()
    {
        try
        {
            var racks = await _repository.GetRacksAsync();
            foreach (var r in racks)
            {
                if (!string.IsNullOrWhiteSpace(r) && !AvailableRacks.Contains(r))
                    AvailableRacks.Add(r);
            }
        }
        catch { }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(BoxNo))
        {
            ErrorMessage = "Box No is required.";
            ShowToast(ErrorMessage, ToastType.Warning);
            return;
        }

        var cleanBoxNo = BoxNo.Trim().ToUpperInvariant();

        try
        {
            IsBusy = true;

            if (IsNew || !string.Equals(_originalBoxNo, cleanBoxNo, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _repository.GetByBoxNoAsync(cleanBoxNo);
                if (existing is not null)
                {
                    ErrorMessage = $"A box with code '{cleanBoxNo}' already exists.";
                    ShowToast(ErrorMessage, ToastType.Warning);
                    return;
                }
            }

            var box = new BoxMaster
            {
                BoxNo = cleanBoxNo,
                BoxName = BoxName?.Trim() ?? string.Empty,
                Rack = Rack?.Trim() ?? string.Empty,
                NumberOfBoxes = NumberOfBoxes > 0 ? NumberOfBoxes : 1,
                BoxWt = BoxWt,
                Serial = Serial > 0 ? Serial : 1,
                Status = string.IsNullOrWhiteSpace(Status) ? "Active" : Status.Trim(),
                Remarks = Remarks?.Trim() ?? string.Empty,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now
            };

            if (IsNew)
            {
                await _repository.AddAsync(box);
            }
            else
            {
                await _repository.UpdateAsync(_originalBoxNo!, box);
            }

            ResultBox = box;
            ShowToast("Box saved successfully.", ToastType.Success);
            await Task.Delay(300);
            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving box: {ex.Message}";
            ShowToast(ErrorMessage, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
