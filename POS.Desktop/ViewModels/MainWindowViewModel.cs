using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Application.Abstractions;
using POS.Application.Auth;
using POS.Application.Cash;
using POS.Application.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly DispatcherTimer _clockTimer;
    private readonly ICurrentSession _session;
    private readonly CashSessionService _cashService;
    private ViewModelBase? _observedVm;

    public CashSessionTracker CashTracker { get; }
    public INavigationService Navigation { get; }

    public MainWindowViewModel(
        INavigationService navigation,
        ICurrentSession session,
        CashSessionService cashService,
        CashSessionTracker cashTracker)
    {
        Navigation = navigation;
        _session = session;
        _cashService = cashService;
        CashTracker = cashTracker;
        GoCatalogCommand = new AsyncRelayCommand(GoCatalogAsync);
        GoSalesCommand = new AsyncRelayCommand(GoSalesAsync);
        GoSettingsCommand = new AsyncRelayCommand(GoSettingsAsync);
        GoUsersCommand = new AsyncRelayCommand(GoUsersAsync);
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
        LogoutCommand = new AsyncRelayCommand(LogoutAsync);

        Navigation.CurrentChanged += _ =>
        {
            // Importante: notificar TODOS los flags, no solo Current. Si no,
            // el sidebar nunca se entera de que cambió la pantalla activa.
            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(IsCatalogActive));
            OnPropertyChanged(nameof(IsSalesActive));
            OnPropertyChanged(nameof(IsSettingsActive));
            OnPropertyChanged(nameof(IsUsersActive));
            ObserveOverlayFlags();
        };

        // Reloj de la barra de título (siempre visible).
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _clockTimer.Tick += (_, _) => OnPropertyChanged(nameof(ClockText));
        _clockTimer.Start();
        OnPropertyChanged(nameof(ClockText));

        // IMPORTANTE: suscribirse al VM inicial. CurrentChanged solo se dispara al
        // NAVEGAR; si el VM inicial ya está activo, sin esto el sidebar nunca
        // reacciona al primer overlay (bug reportado: "no funciona siempre").
        ObserveOverlayFlags();
    }

    /// <summary>Escucha los flags de overlay del VM activo para que el sidebar
    /// "se funda" con el scrim (mismo fondo, sin borde) cuando hay un popup.</summary>
    private void ObserveOverlayFlags()
    {
        if (_observedVm is not null)
            _observedVm.PropertyChanged -= OnVmPropertyChanged;
        _observedVm = Navigation.Current;
        if (_observedVm is not null)
            _observedVm.PropertyChanged += OnVmPropertyChanged;
        UpdateIsAnyOverlayOpen();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SaleViewModel.IsCatalogOpen):
            case nameof(SaleViewModel.IsPaymentOpen):
            case nameof(SaleViewModel.IsResultOpen):
            case nameof(ProductListViewModel.IsCategoryManagerOpen):
            case nameof(ProductListViewModel.IsProductEditorOpen):
                UpdateIsAnyOverlayOpen();
                break;
        }
    }

    /// <summary>True si hay un popup abierto. Con esto el sidebar deja de ser blanco
    /// y se vuelve del mismo color que la vista → el scrim (un solo Grid sobre todo)
    /// se ve perfectamente continuo, sin línea en la frontera.</summary>
    [ObservableProperty]
    private bool _isAnyOverlayOpen;

    private void UpdateIsAnyOverlayOpen()
    {
        IsAnyOverlayOpen = Navigation.Current switch
        {
            SaleViewModel s => s.IsCatalogOpen || s.IsPaymentOpen || s.IsResultOpen || IsAnyCashModalOpen,
            ProductListViewModel p => p.IsCategoryManagerOpen || p.IsProductEditorOpen,
            _ => IsAnyCashModalOpen,
        };
    }

    /// <summary>Hora actual para la barra de título (formato 24h).</summary>
    public string ClockText => DateTime.Now.ToString("HH:mm");

    private async Task GoCatalogAsync()
    {
        try { await Navigation.NavigateToAsync<ProductListViewModel>(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Catálogo: {ex}"); }
    }

    private async Task GoSalesAsync()
    {
        try { await Navigation.NavigateToAsync<SaleViewModel>(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Ventas: {ex}"); }
    }

    private async Task GoSettingsAsync()
    {
        try { await Navigation.NavigateToAsync<SettingsViewModel>(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Ajustes: {ex}"); }
    }

    private async Task GoUsersAsync()
    {
        try { await Navigation.NavigateToAsync<UsersViewModel>(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Usuarios: {ex}"); }
    }

    /// <summary>Vista actual; el ContentControl del MainWindow bindea aquí.</summary>
    public ViewModelBase? Current => Navigation.Current;

    public AsyncRelayCommand GoCatalogCommand { get; }
    public AsyncRelayCommand GoSalesCommand { get; }
    public AsyncRelayCommand GoSettingsCommand { get; }
    public AsyncRelayCommand GoUsersCommand { get; }
    public RelayCommand ToggleSidebarCommand { get; }
    public AsyncRelayCommand LogoutCommand { get; }

    /// <summary>Sidebar colapsado a solo iconos (64px) o expandido (210px).</summary>
    [ObservableProperty]
    private bool _isSidebarCollapsed;

    /// <summary>Ancho del sidebar en DIPs: 210 expandido / 64 solo iconos.</summary>
    public double SidebarWidth => IsSidebarCollapsed ? 64 : 210;

    /// <summary>Item de navegación activo según la vista actual.</summary>
    /// <remarks>El editor de producto es un popup dentro de la lista; no cambia la sección.</remarks>
    public bool IsCatalogActive => Current is ProductListViewModel;
    public bool IsSalesActive => Current is SaleViewModel;
    public bool IsSettingsActive => Current is SettingsViewModel;
    public bool IsUsersActive => Current is UsersViewModel;

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(IsCatalogActive));
        OnPropertyChanged(nameof(IsSalesActive));
        OnPropertyChanged(nameof(IsSettingsActive));
        OnPropertyChanged(nameof(IsUsersActive));
    }

    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    // ─────────────────────────── Sesión / permisos (P2.1) ───────────────────────────

    /// <summary>True si hay sesión activa (oculta sidebar/header de sesión en el login).</summary>
    public bool IsAuthenticated => _session.IsAuthenticated;

    /// <summary>Nombre del usuario autenticado (header y footer del sidebar).</summary>
    public string UserName => _session.CurrentUser?.DisplayName ?? "Sin sesión";

    /// <summary>Rol del usuario autenticado, en español.</summary>
    public string UserRoleText => _session.CurrentUser?.Role.ToString() ?? "—";

    /// <summary>Inicial del nombre para el avatar circular.</summary>
    public string UserInitial => string.IsNullOrEmpty(UserName) ? "?" : UserName[..1].ToUpperInvariant();

    /// <summary>¿El usuario puede gestionar catálogo (P2.1: sidebar dinámico)?</summary>
    public bool CanManageCatalog => _session.CurrentUser is { } u && Permissions.Has(u.Role, Permissions.ManageCatalog);

    /// <summary>¿El usuario puede gestionar usuarios? Solo Admin.</summary>
    public bool CanManageUsers => _session.CurrentUser is { } u && Permissions.Has(u.Role, Permissions.ManageUsers);

    /// <summary>¿El usuario puede ver los ajustes? (imprimir/configuración del negocio).</summary>
    public bool CanManageSettings => _session.CurrentUser is { } u && Permissions.Has(u.Role, Permissions.ManageSettings);

    /// <summary>¿El usuario puede cerrar caja? Controla el botón de cierre en el header.</summary>
    public bool CanCloseCash => _session.CurrentUser is { } u && Permissions.Has(u.Role, Permissions.CloseCash);

    /// <summary>Se notifica cuando cambia la sesión (login/logout).</summary>
    public void NotifySessionChanged()
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(UserName));
        OnPropertyChanged(nameof(UserRoleText));
        OnPropertyChanged(nameof(UserInitial));
        OnPropertyChanged(nameof(CanManageCatalog));
        OnPropertyChanged(nameof(CanManageUsers));
        OnPropertyChanged(nameof(CanManageSettings));
        OnPropertyChanged(nameof(CanCloseCash));
    }

    /// <summary>Cerrar sesión: limpia la sesión, la caja visible y vuelve al login.</summary>
    private async Task LogoutAsync()
    {
        _session.SignOut();
        CashTracker.Set(null);
        NotifySessionChanged();
        try { await Navigation.NavigateToAsync<LoginViewModel>(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Logout: {ex}"); }
    }

    // ─────────────────────────── Caja (P2.2) ───────────────────────────

    /// <summary>Alguna modal de caja abierta (el sidebar se funde con el scrim).</summary>
    public bool IsAnyCashModalOpen => IsCashOpenModal || IsWithdrawModalOpen || IsCloseCashModalOpen;

    [ObservableProperty]
    private bool _isCashOpenModal;

    [ObservableProperty]
    private string _openCashText = string.Empty;

    [ObservableProperty]
    private string? _cashError;

    [ObservableProperty]
    private bool _isWithdrawModalOpen;

    [ObservableProperty]
    private string _withdrawAmountText = string.Empty;

    [ObservableProperty]
    private string _withdrawReason = string.Empty;

    [ObservableProperty]
    private bool _isCloseCashModalOpen;

    [ObservableProperty]
    private string _closeCountText = string.Empty;

    [ObservableProperty]
    private string? _lastCloseResult;

    [RelayCommand]
    private void ClearLastCloseResult() => LastCloseResult = null;

    partial void OnIsCashOpenModalChanged(bool value) => NotifyCashModalChanged();
    partial void OnIsWithdrawModalOpenChanged(bool value) => NotifyCashModalChanged();
    partial void OnIsCloseCashModalOpenChanged(bool value) => NotifyCashModalChanged();

    private void NotifyCashModalChanged()
    {
        OnPropertyChanged(nameof(IsAnyCashModalOpen));
        UpdateIsAnyOverlayOpen();
    }

    [RelayCommand]
    private void OpenCashModal()
    {
        CashError = null;
        LastCloseResult = null;
        OpenCashText = "0";
        IsCashOpenModal = true;
    }

    [RelayCommand]
    private void CloseCashModal() => IsCashOpenModal = false;

    [RelayCommand]
    private async Task ConfirmOpenCashAsync()
    {
        CashError = null;
        var initial = ParseAmount(OpenCashText);
        if (initial < 0)
        {
            CashError = "El fondo inicial no puede ser negativo.";
            return;
        }
        var result = await _cashService.OpenAsync(new OpenCashRequest(_session.CurrentUserId, initial));
        if (!result.IsSuccess)
        {
            CashError = result.ErrorMessage;
            return;
        }
        IsCashOpenModal = false;
        await RefreshCashAsync();
    }

    [RelayCommand]
    private void OpenWithdrawModal()
    {
        CashError = null;
        WithdrawAmountText = string.Empty;
        WithdrawReason = string.Empty;
        IsWithdrawModalOpen = true;
    }

    [RelayCommand]
    private void CloseWithdrawModal() => IsWithdrawModalOpen = false;

    [RelayCommand]
    private async Task ConfirmWithdrawAsync()
    {
        if (CashTracker.Current is null) return;
        CashError = null;
        var amount = ParseAmount(WithdrawAmountText);
        if (amount <= 0)
        {
            CashError = "Ingrese un monto válido.";
            return;
        }
        if (string.IsNullOrWhiteSpace(WithdrawReason))
        {
            CashError = "El motivo es obligatorio.";
            return;
        }
        var result = await _cashService.WithdrawAsync(new WithdrawRequest(
            CashTracker.Current.Id, amount, WithdrawReason.Trim()));
        if (!result.IsSuccess)
        {
            CashError = result.ErrorMessage;
            return;
        }
        IsWithdrawModalOpen = false;
        await RefreshCashAsync();
    }

    [RelayCommand]
    private void OpenCloseCashModal()
    {
        if (CashTracker.Current is null) return;
        CashError = null;
        LastCloseResult = null;
        CloseCountText = CashTracker.Current.ExpectedCash.ToString("N2");
        IsCloseCashModalOpen = true;
    }

    [RelayCommand]
    private void CloseCloseCashModal() => IsCloseCashModalOpen = false;

    [RelayCommand]
    private async Task ConfirmCloseCashAsync()
    {
        if (CashTracker.Current is null) return;
        CashError = null;
        var count = ParseAmount(CloseCountText);
        if (count < 0)
        {
            CashError = "El conteo no puede ser negativo.";
            return;
        }
        var result = await _cashService.CloseAsync(new CloseCashRequest(
            CashTracker.Current.Id, count));
        if (!result.IsSuccess)
        {
            CashError = result.ErrorMessage;
            return;
        }
        IsCloseCashModalOpen = false;
        if (result.Value is { } closed)
            LastCloseResult = $"Caja #{closed.Id} cerrada · Diferencia RD$ {closed.Difference:N2}";
        await RefreshCashAsync();
    }

    private static decimal ParseAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var normalized = text.Replace(',', '.').Trim();
        return decimal.TryParse(normalized, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    /// <summary>Refresca la caja abierta del usuario desde la DB (tras abrir/retirar/cerrar).</summary>
    public async Task RefreshCashAsync()
    {
        try
        {
            var session = await _cashService.GetOpenForUserAsync(_session.CurrentUserId);
            CashTracker.Set(session);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] RefreshCash: {ex}");
        }
    }
}