using POS.Domain.Entities;

namespace POS.Application.Abstractions;

/// <summary>
/// Sesión actual (P2.1b): el usuario autenticado activo. Reemplaza el
/// <c>DemoUserId = 1</c> — sin login no hay sesión y la venta se bloquea.
/// Singleton en la app; se asigna al iniciar sesión y se limpia al cerrar sesión.
/// </summary>
public interface ICurrentSession
{
    User? CurrentUser { get; }
    bool IsAuthenticated => CurrentUser is not null;
    long CurrentUserId => CurrentUser?.Id ?? 0;
    void SignIn(User user);
    void SignOut();
}