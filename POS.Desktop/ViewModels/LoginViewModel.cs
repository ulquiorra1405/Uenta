using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Application.Abstractions;
using POS.Application.Auth;
using POS.Desktop.ViewModels;

namespace POS.Desktop.ViewModels;

/// <summary>
/// Pantalla de login (P2.1c). Al validar credenciales firma la sesión
/// (<see cref="ICurrentSession"/>) y navega a la pantalla de venta.
/// </summary>
public partial class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly ICurrentSession _session;
    private readonly INavigationService _navigation;
    private readonly MainWindowViewModel _mainWindow;

    public LoginViewModel(AuthService authService, ICurrentSession session, INavigationService navigation,
        MainWindowViewModel mainWindow)
    {
        _authService = authService;
        _session = session;
        _navigation = navigation;
        _mainWindow = mainWindow;
        LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsBusy);
    }

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public AsyncRelayCommand LoginCommand { get; }

    [RelayCommand]
    private void ClearError() => ErrorMessage = null;

    private async Task LoginAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        IsBusy = true;
        LoginCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await _authService.ValidateAsync(Username, Password);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            _session.SignIn(result.User!);
            Password = string.Empty;
            _mainWindow.NotifySessionChanged();
            await _navigation.NavigateToAsync<SaleViewModel>();
        }
        finally
        {
            IsBusy = false;
            LoginCommand.NotifyCanExecuteChanged();
        }
    }
}