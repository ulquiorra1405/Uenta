using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Application.Customers;

namespace POS.Desktop.ViewModels;

/// <summary>Item de la lista de clientes (P4.1).</summary>
public partial class CustomerListItem : ObservableObject
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string RncCedula { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editPhone = string.Empty;

    [ObservableProperty]
    private string _editRncCedula = string.Empty;

    [ObservableProperty]
    private string _editEmail = string.Empty;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isBusy;
}

/// <summary>Compra del historial del cliente (P4.1).</summary>
public partial class CustomerSaleListItem : ObservableObject
{
    public long Number { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public decimal Total { get; init; }
    public int ItemCount { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string DateText => CreatedAt.ToString("dd/MM/yyyy HH:mm");
    public string TotalText => $"RD$ {Total:N2}";
    public string MetaText => $"{DateText} · {ItemCount} líneas · {UserName}";
}

/// <summary>
/// CRM básico (P4.1): lista de clientes, crear/editar y ver historial de compras.
/// Accesible a cualquier rol con permiso de venta (todos los roles).
/// </summary>
public partial class CustomersViewModel : ViewModelBase
{
    private readonly CustomerService _customerService;

    public ObservableCollection<CustomerListItem> Customers { get; } = [];
    public ObservableCollection<CustomerSaleListItem> HistorySales { get; } = [];

    [ObservableProperty]
    private bool _isCreateOpen;

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _newPhone = string.Empty;

    [ObservableProperty]
    private string _newRncCedula = string.Empty;

    [ObservableProperty]
    private string _newEmail = string.Empty;

    [ObservableProperty]
    private string? _createMessage;

    [ObservableProperty]
    private string? _createError;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private bool _isHistoryOpen;

    [ObservableProperty]
    private string _historyCustomerName = string.Empty;

    [ObservableProperty]
    private string? _historyMessage;

    [ObservableProperty]
    private bool _isHistoryBusy;

    [ObservableProperty]
    private string? _loadError;

    public CustomersViewModel(CustomerService customerService)
    {
        _customerService = customerService;
        CreateCommand = new AsyncRelayCommand(CreateAsync, () => !IsCreating);
    }

    public AsyncRelayCommand CreateCommand { get; }

    [RelayCommand]
    private void OpenCreate()
    {
        CreateError = null;
        CreateMessage = null;
        IsCreateOpen = true;
    }

    [RelayCommand]
    private void CloseCreate() => IsCreateOpen = false;

    private async Task CreateAsync()
    {
        if (IsCreating) return;
        CreateError = null;
        CreateMessage = null;

        if (string.IsNullOrWhiteSpace(NewName))
        {
            CreateError = "El nombre del cliente es obligatorio.";
            return;
        }

        IsCreating = true;
        try
        {
            var result = await _customerService.CreateAsync(new CreateCustomerRequest(
                NewName, NewPhone, NewRncCedula, NewEmail));
            if (!result.IsSuccess)
            {
                CreateError = result.ErrorMessage;
                return;
            }

            CreateMessage = $"Cliente '{result.Value!.Name}' registrado.";
            NewName = string.Empty;
            NewPhone = string.Empty;
            NewRncCedula = string.Empty;
            NewEmail = string.Empty;
            await LoadAsync();
        }
        finally
        {
            IsCreating = false;
        }
    }

    [RelayCommand]
    private void BeginEdit(CustomerListItem? item)
    {
        if (item is null) return;
        item.EditName = item.Name;
        item.EditPhone = item.Phone;
        item.EditRncCedula = item.RncCedula;
        item.EditEmail = item.Email;
        item.IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveEditAsync(CustomerListItem? item)
    {
        if (item is null || item.IsBusy) return;
        item.IsBusy = true;
        item.Message = null;
        try
        {
            var result = await _customerService.UpdateAsync(new UpdateCustomerRequest(
                item.Id, item.EditName, item.EditPhone, item.EditRncCedula, item.EditEmail));
            if (!result.IsSuccess)
            {
                item.Message = result.ErrorMessage;
                return;
            }
            item.IsEditing = false;
            item.Message = "Guardado.";
            await LoadAsync();
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelEdit(CustomerListItem? item)
    {
        if (item is null) return;
        item.IsEditing = false;
        item.Message = null;
    }

    [RelayCommand]
    private async Task ShowHistoryAsync(CustomerListItem? item)
    {
        if (item is null || IsHistoryBusy) return;
        HistorySales.Clear();
        HistoryCustomerName = item.Name;
        HistoryMessage = null;
        IsHistoryOpen = true;
        IsHistoryBusy = true;
        try
        {
            var result = await _customerService.GetHistoryAsync(item.Id);
            if (!result.IsSuccess)
            {
                HistoryMessage = result.ErrorMessage;
                return;
            }
            foreach (var s in result.Value!)
            {
                HistorySales.Add(new CustomerSaleListItem
                {
                    Number = s.Number,
                    CreatedAt = s.CreatedAt,
                    Total = s.Total.Amount,
                    ItemCount = s.ItemCount,
                    UserName = s.UserName,
                });
            }
            if (HistorySales.Count == 0)
                HistoryMessage = "Este cliente aún no tiene compras.";
        }
        catch (Exception ex)
        {
            HistoryMessage = $"No se pudo cargar el historial: {ex.Message}";
        }
        finally
        {
            IsHistoryBusy = false;
        }
    }

    [RelayCommand]
    private void CloseHistory() => IsHistoryOpen = false;

    public override async Task OnNavigatedToAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        LoadError = null;
        try
        {
            var all = await _customerService.GetAllAsync();
            Customers.Clear();
            foreach (var c in all)
            {
                Customers.Add(new CustomerListItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Phone = c.Phone,
                    RncCedula = c.RncCedula,
                    Email = c.Email,
                });
            }
        }
        catch (Exception ex)
        {
            LoadError = $"No se pudieron cargar los clientes: {ex.Message}";
        }
    }
}