using CommunityToolkit.Mvvm.ComponentModel;

namespace POS.Desktop.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>Se invoca al navegar hacia este ViewModel (recarga de datos).</summary>
    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;
}
