using POS.Application.Abstractions;
using POS.Domain.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Sesión actual (P2.1b): singleton con el usuario autenticado.
/// Sin login → CurrentUser null → la venta y las acciones protegidas se bloquean.
/// </summary>
public class CurrentSession : ICurrentSession
{
    public User? CurrentUser { get; private set; }

    public void SignIn(User user) => CurrentUser = user;

    public void SignOut() => CurrentUser = null;
}