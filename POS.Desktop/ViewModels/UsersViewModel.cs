using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Application.Abstractions;
using POS.Application.Auth;
using POS.Application.Common;
using POS.Domain.Enums;

namespace POS.Desktop.ViewModels;

/// <summary>Item de la lista de usuarios (P2.1e).</summary>
public partial class UserListItem : ObservableObject
{
    public long Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public string RoleText => Role switch
    {
        UserRole.Admin => "Administrador",
        UserRole.Supervisor => "Supervisor",
        _ => "Cajero"
    };
    public bool IsSelf { get; init; }
    public bool CanEdit => !IsSelf;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editDisplayName = string.Empty;

    /// <summary>Rol seleccionado en el editor (opción completa, no solo el enum).</summary>
    [ObservableProperty]
    private RoleOption? _editRoleOption;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isBusy;
}

/// <summary>Opción de rol para los ComboBox (texto + valor enum).</summary>
public record RoleOption(UserRole Value, string Label);

/// <summary>
/// Gestión de usuarios (P2.1e): lista + crear + activar/desactivar + reset de
/// contraseña. Solo visible para Admin (permiso ManageUsers).
/// </summary>
public partial class UsersViewModel : ViewModelBase
{
    private readonly UserService _userService;
    private readonly ICurrentSession _session;

    public ObservableCollection<UserListItem> Users { get; } = [];

    /// <summary>Opciones de rol para el formulario de creación.</summary>
    public IReadOnlyList<RoleOption> RoleOptions { get; } =
    [
        new(UserRole.Admin, "Administrador"),
        new(UserRole.Supervisor, "Supervisor"),
        new(UserRole.Cajero, "Cajero")
    ];

    /// <summary>Rol seleccionado en el formulario de creación (por defecto Cajero).</summary>
    [ObservableProperty]
    private RoleOption _selectedNewRole = new(UserRole.Cajero, "Cajero");

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newDisplayName = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private bool _isCreateOpen;

    [ObservableProperty]
    private string? _createMessage;

    [ObservableProperty]
    private string? _createError;

    public UsersViewModel(UserService userService, ICurrentSession session)
    {
        _userService = userService;
        _session = session;
        CreateCommand = new AsyncRelayCommand(CreateAsync, () => !IsCreateOpen);
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
        if (IsCreateOpen) return;
        CreateError = null;
        CreateMessage = null;

        if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewPassword))
        {
            CreateError = "Complete usuario y contraseña.";
            return;
        }

        var result = await _userService.CreateAsync(new CreateUserRequest(
            NewUsername, string.IsNullOrWhiteSpace(NewDisplayName) ? NewUsername : NewDisplayName,
            NewPassword, SelectedNewRole.Value));
        if (!result.IsSuccess)
        {
            CreateError = result.ErrorMessage;
            return;
        }

        CreateMessage = $"Usuario '{result.Value!.Username}' creado.";
        NewUsername = string.Empty;
        NewDisplayName = string.Empty;
        NewPassword = string.Empty;
        SelectedNewRole = RoleOptions[2];
        await LoadAsync();
    }

    [RelayCommand]
    private void BeginEdit(UserListItem? item)
    {
        if (item is null || !item.CanEdit) return;
        item.EditDisplayName = item.DisplayName;
        item.EditRoleOption = RoleOptions.FirstOrDefault(r => r.Value == item.Role);
        item.IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveEditAsync(UserListItem? item)
    {
        if (item is null || !item.CanEdit || item.IsBusy) return;
        item.IsBusy = true;
        item.Message = null;
        try
        {
            var result = await _userService.UpdateAsync(new UpdateUserRequest(
                item.Id, item.EditDisplayName, item.EditRoleOption?.Value ?? item.Role, item.IsActive));
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
    private void CancelEdit(UserListItem? item)
    {
        if (item is null) return;
        item.IsEditing = false;
        item.Message = null;
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(UserListItem? item)
    {
        if (item is null || !item.CanEdit || item.IsBusy) return;
        item.IsBusy = true;
        item.Message = null;
        try
        {
            var result = await _userService.UpdateAsync(new UpdateUserRequest(
                item.Id, item.DisplayName, item.Role, !item.IsActive));
            if (!result.IsSuccess)
            {
                item.Message = result.ErrorMessage;
                return;
            }
            await LoadAsync();
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetPasswordAsync(UserListItem? item)
    {
        if (item is null || !item.CanEdit) return;
        var password = Microsoft.VisualBasic.Interaction.InputBox(
            $"Nueva contraseña para '{item.Username}' (mín. 6 caracteres):", "Reset de contraseña", "");
        if (string.IsNullOrWhiteSpace(password)) return;

        var result = await _userService.ResetPasswordAsync(new ResetPasswordRequest(item.Id, password));
        item.Message = result.IsSuccess ? "Contraseña actualizada." : result.ErrorMessage;
    }

    public override async Task OnNavigatedToAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            var currentId = _session.CurrentUserId;
            var all = await _userService.GetAllAsync();
            Users.Clear();
            foreach (var u in all)
            {
                Users.Add(new UserListItem
                {
                    Id = u.Id,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    IsSelf = u.Id == currentId
                });
            }
        }
        catch (Exception ex)
        {
            // La lista vacía con un mensaje visible es mejor que fallar en silencio.
            CreateError = $"No se pudieron cargar los usuarios: {ex.Message}";
        }
    }
}