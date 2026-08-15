using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Auth;

public record CreateUserRequest(string Username, string DisplayName, string Password, UserRole Role);
public record UpdateUserRequest(long Id, string DisplayName, UserRole Role, bool IsActive);
public record ResetPasswordRequest(long Id, string NewPassword);

public record UserDto
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Caso de uso: gestión de usuarios (P2.1e). SOLO Admin — la UI lo protege con
/// el permiso ManageUsers y el servicio valida que el usuario exista al editar.
/// El hash lo aplica <see cref="IPasswordHasher"/>; la contraseña NUNCA viaja en
/// los DTO de salida.
/// </summary>
public class UserService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;

    public UserService(IUserRepository users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    public async Task<List<UserDto>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await _users.GetAllAsync(ct);
        return users.Select(ToDto).ToList();
    }

    public async Task<Result<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return Result.Failure<UserDto>("USERNAME_REQUIRED", "El nombre de usuario es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return Result.Failure<UserDto>("DISPLAY_NAME_REQUIRED", "El nombre para mostrar es obligatorio.");
        if (request.Password.Length < 6)
            return Result.Failure<UserDto>("WEAK_PASSWORD", "La contraseña debe tener al menos 6 caracteres.");
        if (await _users.UsernameExistsAsync(request.Username.Trim(), ct))
            return Result.Failure<UserDto>("USERNAME_DUPLICATED", "Ya existe un usuario con ese nombre.");

        var user = new User
        {
            Username = request.Username.Trim(),
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTimeOffset.Now
        };

        await _users.AddAsync(user, ct);
        return Result.Success(ToDto(user));
    }

    public async Task<Result<UserDto>> UpdateAsync(UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(request.Id, ct);
        if (user is null)
            return Result.Failure<UserDto>("USER_NOT_FOUND", "El usuario no existe.");
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return Result.Failure<UserDto>("DISPLAY_NAME_REQUIRED", "El nombre para mostrar es obligatorio.");

        user.DisplayName = request.DisplayName.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;

        await _users.UpdateAsync(user, ct);
        return Result.Success(ToDto(user));
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(request.Id, ct);
        if (user is null)
            return Result.Failure("USER_NOT_FOUND", "El usuario no existe.");
        if (request.NewPassword.Length < 6)
            return Result.Failure("WEAK_PASSWORD", "La contraseña debe tener al menos 6 caracteres.");

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        await _users.UpdateAsync(user, ct);
        return Result.Success();
    }

    private static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        DisplayName = u.DisplayName,
        Role = u.Role,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt
    };
}