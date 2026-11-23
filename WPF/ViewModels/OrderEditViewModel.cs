using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Shared;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class OrderEditViewModel : ViewModelBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository? _customerRepository;
    private readonly IItemRepository? _itemRepository;
    private readonly IPurityRepository? _purityRepository;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;
    private readonly bool _isNew;

    private string _orderNo = string.Empty;
    private string _orderCategory = "Order";
    private int _nos;
    private DateTime _orderDate = DateTime.Today;
    private DateTime? _deliveryDate = DateTime.Today.AddDays(7);

    private string _customerName = string.Empty;
    private string _phone = string.Empty;
    private string _address = string.Empty;
    private string _address2 = string.Empty;
    private string _city = string.Empty;

    private string _itemName = string.Empty;
    private decimal _qty = 1m;
    private string _size = string.Empty;
    private string _description = string.Empty;
    private string _purity = "22K";
    private decimal _appWeight;
    private decimal _makPerGr;
    private decimal _making;
    private decimal _metalRate;
    private decimal _appValue;
    private string _rateType = "Fix";
    private decimal _advancePaid;

    private string _stoneName = string.Empty;
    private decimal _stoneWt;
    private decimal _stoneValue;

    private string _karigarName = string.Empty;
    private string _company = string.Empty;
    private string _styleNo = string.Empty;
    private string _cCode = string.Empty;
    private string _companyPerson = string.Empty;

    private string _photo = string.Empty;
    private string? _photoPreviewPath;
    private string? _tempUploadedFilePath;

    private string _status = "Pending";
    private bool _closed;

    private bool _isBusy;
    private string? _errorMessage;

    public event EventHandler<bool?>? RequestClose;

    public OrderEditViewModel(
        IOrderRepository orderRepository,
        ICustomerRepository? customerRepository,
        IItemRepository? itemRepository,
        IPurityRepository? purityRepository,
        ISettingsRepository? settingsRepository,
        string currentUser,
        CustomerOrder? existingOrder = null,
        IAuditRepository? auditRepository = null)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _itemRepository = itemRepository;
        _purityRepository = purityRepository;
        _settingsRepository = settingsRepository;
        _currentUser = currentUser;
        _auditRepository = auditRepository;
        _isNew = existingOrder == null;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel);
        BrowseImageCommand = new RelayCommand(BrowseImage);
        PasteImageCommand = new RelayCommand(PasteImage);
        ClearImageCommand = new RelayCommand(ClearImage);

        PopulateDropdowns();

        if (existingOrder != null)
        {
            LoadExistingOrder(existingOrder);
        }
        else
        {
            _ = InitNewOrderAsync();
        }
    }

    public bool IsNew => _isNew;
    public string Title => _isNew ? "New Customer Requirement / Order" : $"Modify Order #{OrderNo}";

    public string OrderNo
    {
        get => _orderNo;
        set => SetProperty(ref _orderNo, value);
    }

    public string OrderCategory
    {
        get => _orderCategory;
        set => SetProperty(ref _orderCategory, value);
    }

    public ObservableCollection<string> OrderCategoryOptions { get; } = new()
    {
        "Order",
        "Requirement"
    };

    public int Nos
    {
        get => _nos;
        set => SetProperty(ref _nos, value);
    }

    public DateTime OrderDate
    {
        get => _orderDate;
        set => SetProperty(ref _orderDate, value);
    }

    public DateTime? DeliveryDate
    {
        get => _deliveryDate;
        set => SetProperty(ref _deliveryDate, value);
    }

    public string CustomerName
    {
        get => _customerName;
        set
        {
            if (SetProperty(ref _customerName, value))
            {
                OnCustomerNameChanged();
            }
        }
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public string Address2
    {
        get => _address2;
        set => SetProperty(ref _address2, value);
    }

    public string City
    {
        get => _city;
        set => SetProperty(ref _city, value);
    }

    public string ItemName
    {
        get => _itemName;
        set => SetProperty(ref _itemName, value);
    }

    public decimal Qty
    {
        get => _qty;
        set => SetProperty(ref _qty, value);
    }

    public string Size
    {
        get => _size;
        set => SetProperty(ref _size, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string Purity
    {
        get => _purity;
        set
        {
            if (SetProperty(ref _purity, value))
            {
                _ = OnPurityChangedAsync();
            }
        }
    }

    public decimal AppWeight
    {
        get => _appWeight;
        set
        {
            if (SetProperty(ref _appWeight, value))
            {
                RecalculateCalculations();
            }
        }
    }

    public decimal MakPerGr
    {
        get => _makPerGr;
        set
        {
            if (SetProperty(ref _makPerGr, value))
            {
                Making = Math.Round(_appWeight * _makPerGr, 2);
                RecalculateCalculations();
            }
        }
    }

    public decimal Making
    {
        get => _making;
        set
        {
            if (SetProperty(ref _making, value))
            {
                RecalculateCalculations();
            }
        }
    }

    public decimal MetalRate
    {
        get => _metalRate;
        set
        {
            if (SetProperty(ref _metalRate, value))
            {
                RecalculateCalculations();
            }
        }
    }

    public decimal AppValue
    {
        get => _appValue;
        set
        {
            if (SetProperty(ref _appValue, value))
            {
                OnPropertyChanged(nameof(BalanceDue));
            }
        }
    }

    public string RateType
    {
        get => _rateType;
        set => SetProperty(ref _rateType, value);
    }

    public decimal AdvancePaid
    {
        get => _advancePaid;
        set
        {
            if (SetProperty(ref _advancePaid, value))
            {
                OnPropertyChanged(nameof(BalanceDue));
            }
        }
    }

    public decimal BalanceDue => Math.Max(0m, _appValue - _advancePaid);

    public string StoneName
    {
        get => _stoneName;
        set => SetProperty(ref _stoneName, value);
    }

    public decimal StoneWt
    {
        get => _stoneWt;
        set => SetProperty(ref _stoneWt, value);
    }

    public decimal StoneValue
    {
        get => _stoneValue;
        set
        {
            if (SetProperty(ref _stoneValue, value))
            {
                RecalculateCalculations();
            }
        }
    }

    public string KarigarName
    {
        get => _karigarName;
        set => SetProperty(ref _karigarName, value);
    }

    public string Company
    {
        get => _company;
        set => SetProperty(ref _company, value);
    }

    public string StyleNo
    {
        get => _styleNo;
        set => SetProperty(ref _styleNo, value);
    }

    public string CCode
    {
        get => _cCode;
        set => SetProperty(ref _cCode, value);
    }

    public string CompanyPerson
    {
        get => _companyPerson;
        set => SetProperty(ref _companyPerson, value);
    }

    public string Photo
    {
        get => _photo;
        set => SetProperty(ref _photo, value);
    }

    public string? PhotoPreviewPath
    {
        get => _photoPreviewPath;
        set
        {
            if (SetProperty(ref _photoPreviewPath, value))
            {
                OnPropertyChanged(nameof(HasPhoto));
            }
        }
    }

    public bool HasPhoto => !string.IsNullOrWhiteSpace(_photoPreviewPath) && File.Exists(_photoPreviewPath);

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool Closed
    {
        get => _closed;
        set => SetProperty(ref _closed, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsOrderNoReadOnly => !_isNew;

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(_errorMessage);

    public ObservableCollection<string> AvailableItemNames { get; } = new();
    public ObservableCollection<string> AvailablePurityNames { get; } = new();
    public ObservableCollection<string> AvailableCustomers { get; } = new();
    public ObservableCollection<string> AvailableKarighars { get; } = new();
    public ObservableCollection<string> AvailableStatuses { get; } = new()
    {
        "Pending", "In Progress", "Completed", "Delivered", "Cancelled"
    };

    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand BrowseImageCommand { get; }
    public RelayCommand PasteImageCommand { get; }
    public RelayCommand ClearImageCommand { get; }

    private void PopulateDropdowns()
    {
        if (_itemRepository != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var items = await _itemRepository.GetAllAsync();
                    var itemNames = items.Select(i => i.ItemName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        AvailableItemNames.Clear();
                        foreach (var name in itemNames) AvailableItemNames.Add(name);
                    });
                }
                catch { }
            });
        }

        if (_purityRepository != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var purities = await _purityRepository.GetAllAsync();
                    var pNames = purities.Select(p => p.Purity).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        AvailablePurityNames.Clear();
                        foreach (var p in pNames) AvailablePurityNames.Add(p);
                    });
                }
                catch { }
            });
        }

        if (_customerRepository != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var customers = await _customerRepository.GetAllAsync();
                    var cNames = customers.Select(c => c.CompanyName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        AvailableCustomers.Clear();
                        foreach (var c in cNames) AvailableCustomers.Add(c);
                    });
                }
                catch { }
            });
        }
    }

    private void OnCustomerNameChanged()
    {
        if (string.IsNullOrWhiteSpace(_customerName) || _customerRepository == null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var customers = await _customerRepository.GetAllAsync();
                var match = customers.FirstOrDefault(c => string.Equals(c.CompanyName, _customerName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (string.IsNullOrWhiteSpace(Phone) && !string.IsNullOrWhiteSpace(match.Phone)) Phone = match.Phone;
                        if (string.IsNullOrWhiteSpace(Phone) && !string.IsNullOrWhiteSpace(match.Mobile)) Phone = match.Mobile;
                        if (string.IsNullOrWhiteSpace(Address) && !string.IsNullOrWhiteSpace(match.Address)) Address = match.Address;
                        if (string.IsNullOrWhiteSpace(Address2) && !string.IsNullOrWhiteSpace(match.Address2)) Address2 = match.Address2;
                        if (string.IsNullOrWhiteSpace(City) && !string.IsNullOrWhiteSpace(match.City)) City = match.City;
                    });
                }
            }
            catch { }
        });
    }

    private async Task OnPurityChangedAsync()
    {
        if (string.IsNullOrWhiteSpace(_purity) || _purityRepository == null) return;

        try
        {
            var purities = await _purityRepository.GetAllAsync();
            var match = purities.FirstOrDefault(p => string.Equals(p.Purity, _purity, StringComparison.OrdinalIgnoreCase));
            if (match != null && match.RatePerGram > 0)
            {
                MetalRate = match.RatePerGram;
            }
        }
        catch { }
    }

    private void RecalculateCalculations()
    {
        if (_makPerGr > 0 && _appWeight > 0 && _making == 0)
        {
            _making = Math.Round(_appWeight * _makPerGr, 2);
            OnPropertyChanged(nameof(Making));
        }

        var metalVal = Math.Round(_appWeight * _metalRate, 2);
        AppValue = Math.Round(metalVal + _making + _stoneValue, 2);
    }

    private async Task InitNewOrderAsync()
    {
        try
        {
            Nos = await _orderRepository.GetNextNosAsync();
            OrderNo = await _orderRepository.GetNextOrderNoAsync();
            await OnPurityChangedAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Could not initialize next order number: " + ex.Message;
        }
    }

    private void LoadExistingOrder(CustomerOrder o)
    {
        _orderNo = o.OrderNo;
        _orderCategory = !string.IsNullOrWhiteSpace(o.OrderCategory) ? o.OrderCategory : "Order";
        _nos = o.Nos;
        _orderDate = o.OrderDate;
        _deliveryDate = o.DeliveryDate ?? o.OrderDeliveryDate;
        _customerName = o.CustomerName;
        _phone = o.Phone;
        _address = o.Address;
        _address2 = o.Address2;
        _city = o.City;
        _itemName = o.ItemName;
        _qty = o.Qty > 0 ? o.Qty : 1m;
        _size = o.Size;
        _description = o.Description;
        _purity = !string.IsNullOrWhiteSpace(o.Purity) ? o.Purity : "22K";
        _appWeight = o.AppWeight;
        _makPerGr = o.MakPerGr;
        _making = o.Making;
        _metalRate = o.MetalRate;
        _appValue = o.AppValue;
        _rateType = !string.IsNullOrWhiteSpace(o.RateType) ? o.RateType : "Fix";
        _advancePaid = o.AdvancePaid;
        _stoneName = o.StoneName;
        _stoneWt = o.StoneWt;
        _stoneValue = o.StoneValue;
        _karigarName = o.KarigarName;
        _company = o.Company;
        _styleNo = o.StyleNo;
        _cCode = o.CCode;
        _companyPerson = o.CompanyPerson;
        _photo = o.Photo;
        _status = !string.IsNullOrWhiteSpace(o.Status) ? o.Status : "Pending";
        _closed = o.Closed;

        PhotoPreviewPath = o.ResolvedPhotoPath;
    }

    private void BrowseImage()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select Requirement / Order Design Image",
            Filter = "Image Files (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            _tempUploadedFilePath = ofd.FileName;
            PhotoPreviewPath = ofd.FileName;
            Photo = Path.GetFileName(ofd.FileName);
        }
    }

    private void PasteImage()
    {
        try
        {
            if (Clipboard.ContainsImage())
            {
                var image = Clipboard.GetImage();
                if (image != null)
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), "ConceptJewelleryPro_Orders");
                    if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                    var tempFile = Path.Combine(tempDir, $"clipboard_order_{DateTime.Now:yyyyMMddHHmmss}.png");

                    using (var fileStream = new FileStream(tempFile, FileMode.Create))
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(image));
                        encoder.Save(fileStream);
                    }

                    _tempUploadedFilePath = tempFile;
                    PhotoPreviewPath = tempFile;
                    Photo = Path.GetFileName(tempFile);
                }
            }
            else
            {
                MessageBox.Show("No image found in clipboard.", "Paste Image", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to paste image: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearImage()
    {
        _tempUploadedFilePath = null;
        PhotoPreviewPath = null;
        Photo = string.Empty;
    }

    private async Task<string?> SaveImageToTargetFolderAsync(string orderNo)
    {
        if (string.IsNullOrWhiteSpace(_tempUploadedFilePath) || !File.Exists(_tempUploadedFilePath))
        {
            return _photo;
        }

        try
        {
            string? savePath = null;
            if (_settingsRepository != null)
            {
                savePath = await _settingsRepository.GetValueAsync(AppSetting.SavePathKey);
            }

            if (string.IsNullOrWhiteSpace(savePath))
            {
                ErrorMessage = "SavePath is not configured in Settings. Please set SavePath in General Settings.";
                return _photo;
            }

            var targetDir = Path.Combine(savePath, "Order");
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            var cleanOrderNo = orderNo.Replace("/", "_").Replace("\\", "_");
            var ext = Path.GetExtension(_tempUploadedFilePath);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

            var targetFileName = $"{cleanOrderNo}{ext}";
            var targetFilePath = Path.Combine(targetDir, targetFileName);

            File.Copy(_tempUploadedFilePath, targetFilePath, overwrite: true);
            return targetFileName;
        }
        catch
        {
            return _photo;
        }
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(OrderNo))
        {
            ErrorMessage = "Order Number is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CustomerName))
        {
            ErrorMessage = "Customer Name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ItemName))
        {
            ErrorMessage = "Item Name is required.";
            return;
        }

        IsBusy = true;
        try
        {
            var savedPhotoName = await SaveImageToTargetFolderAsync(OrderNo);

            var order = new CustomerOrder
            {
                OrderNo = OrderNo.Trim(),
                OrderCategory = string.IsNullOrWhiteSpace(OrderCategory) ? "Order" : OrderCategory.Trim(),
                Nos = Nos,
                OrderDate = OrderDate,
                DeliveryDate = DeliveryDate,
                OrderDeliveryDate = DeliveryDate,
                CustomerName = CustomerName.Trim(),
                Phone = Phone?.Trim() ?? string.Empty,
                Address = Address?.Trim() ?? string.Empty,
                Address2 = Address2?.Trim() ?? string.Empty,
                City = City?.Trim() ?? string.Empty,
                ItemName = ItemName.Trim(),
                Qty = Qty > 0 ? Qty : 1m,
                Size = Size?.Trim() ?? string.Empty,
                Description = Description?.Trim() ?? string.Empty,
                Purity = Purity?.Trim() ?? string.Empty,
                AppWeight = AppWeight,
                MakPerGr = MakPerGr,
                Making = Making,
                MetalRate = MetalRate,
                AppValue = AppValue,
                RateType = RateType ?? "Fix",
                AdvancePaid = AdvancePaid,
                StoneName = StoneName?.Trim() ?? string.Empty,
                StoneWt = StoneWt,
                StoneValue = StoneValue,
                KarigarName = KarigarName?.Trim() ?? string.Empty,
                Company = Company?.Trim() ?? string.Empty,
                StyleNo = StyleNo?.Trim() ?? string.Empty,
                CCode = CCode?.Trim() ?? string.Empty,
                CompanyPerson = CompanyPerson?.Trim() ?? string.Empty,
                Photo = savedPhotoName ?? string.Empty,
                Status = Status ?? "Pending",
                Closed = Closed
            };

            var success = await _orderRepository.SaveAsync(order, _isNew, _currentUser);
            if (success)
            {
                if (_auditRepository != null)
                {
                    _ = _auditRepository.LogAuditAsync(new AuditLogEntry
                    {
                        Date = DateTime.Today,
                        Time = DateTime.Now,
                        User = _currentUser,
                        EntryType = "Customer Order",
                        VouNo = order.OrderNo,
                        FieldName = _isNew ? "Create" : "Update",
                        OldValue = string.Empty,
                        NewValue = $"Order for {order.CustomerName} - {order.ItemName} ({order.AppWeight:N3}g), Adv: {order.AdvancePaid:N2}, Status: {order.Status}"
                    });
                }

                RequestClose?.Invoke(this, true);
            }
            else
            {
                ErrorMessage = "Failed to save order to database.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Save error: " + ex.Message;
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
}
