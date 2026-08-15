using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Auth;

public record LoginResult(User? User, string? ErrorCode, string? ErrorMessage)
{
    public bool IsSuccess => User is not null;
    public bool IsFailure => User is null;
    public static LoginResult Ok(User user) => new(user, null, null);
    public static LoginResult Fail(string code, string message) => new(null, code, message);
}

/// <summary>
/// Caso de uso: IniciarSesión. Valida credenciales contra el hash PBKDF2 y un
/// usuario activo. NO inicia la sesión — eso lo hace la UI llamando a
/// <see cref="ICurrentSession.SignIn"/>. Registra el evento en auditoría.
/// </summary>
public class AuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly IAuditLogRepository _audit;

    public AuthService(IUserRepository users, IPasswordHasher hasher, IClock clock, IAuditLogRepository audit)
    {
        _users = users;
        _hasher = hasher;
        _clock = clock;
        _audit = audit;
    }

    public async Task<LoginResult> ValidateAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return LoginResult.Fail("CREDENTIALS_REQUIRED", "Ingrese usuario y contraseña.");

        var user = await _users.GetByUsernameAsync(username.Trim(), ct);

        // Usuario inexistente o contraseña inválida: mismo mensaje (no revelar cuál falló).
        if (user is null || !_hasher.Verify(password, user.PasswordHash))
        {
            await _audit.AddAsync(new AuditLog
            {
                UserId = 0,
                Username = username.Trim(),
                Action = AuditAction.LoginFailed,
                Detail = "Credenciales inválidas",
                CreatedAt = _clock.Now
            }, ct);
            return LoginResult.Fail("INVALID_CREDENTIALS", "Usuario o contraseña incorrectos.");
        }

        if (!user.IsActive)
            return LoginResult.Fail("USER_INACTIVE", "Este usuario está desactivado. Contacte al administrador.");

        await _audit.AddAsync(new AuditLog
        {
            UserId = user.Id,
            Username = user.Username,
            Action = AuditAction.Login,
            Detail = $"Inicio de sesión ({user.Role})",
            CreatedAt = _clock.Now
        }, ct);

        return LoginResult.Ok(user);
    }
}