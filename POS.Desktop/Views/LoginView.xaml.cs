using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.Desktop.ViewModels;

namespace POS.Desktop.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    /// <summary>Enter en el campo usuario salta a la contraseña (flujo de teclado).</summary>
    private void Username_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PasswordInput.Focus();
            e.Handled = true;
        }
    }

    /// <summary>PasswordBox no es bindable; se sincroniza con el VM en cada cambio.</summary>
    private void Password_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
            vm.Password = PasswordInput.Password;
    }

    /// <summary>Enter en la contraseña dispara el login (el cajero no toca el mouse).</summary>
    private void Password_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is LoginViewModel vm && vm.LoginCommand.CanExecute(null))
        {
            vm.LoginCommand.Execute(null);
            e.Handled = true;
        }
    }
}