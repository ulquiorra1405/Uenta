using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.Desktop.ViewModels;

namespace POS.Desktop.Views;

public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
    }

    /// <summary>Enter en la contraseña del formulario de creación crea el usuario.</summary>
    private void NewPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is UsersViewModel vm && vm.CreateCommand.CanExecute(null))
        {
            vm.CreateCommand.Execute(null);
            e.Handled = true;
        }
    }
}