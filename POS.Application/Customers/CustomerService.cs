using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Application.Customers;

public record CreateCustomerRequest(string Name, string Phone, string RncCedula, string Email);
public record UpdateCustomerRequest(long Id, string Name, string Phone, string RncCedula, string Email);

public record CustomerDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string RncCedula { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public record CustomerSaleDto
{
    public long Number { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Money Total { get; set; }
    public int ItemCount { get; set; }
    public string UserName { get; set; } = string.Empty;
}

/// <summary>
/// Caso de uso: CRM básico (P4.1) — crear/editar clientes, listar y ver historial
/// de compras. La venta asigna <c>CustomerId</c> opcional; null = venta anónima.
/// </summary>
public class CustomerService
{
    private readonly ICustomerRepository _customers;

    public CustomerService(ICustomerRepository customers) => _customers = customers;

    public async Task<List<CustomerDto>> GetAllAsync(CancellationToken ct = default)
    {
        var customers = await _customers.GetAllAsync(ct);
        return customers.Select(ToDto).ToList();
    }

    public async Task<Result<CustomerDto>> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request.Name, request.RncCedula, ct: ct);
        if (validation is not null) return validation;

        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Phone = request.Phone.Trim(),
            RncCedula = request.RncCedula.Trim(),
            Email = request.Email.Trim(),
            CreatedAt = DateTimeOffset.Now,
        };

        await _customers.AddAsync(customer, ct);
        return Result.Success(ToDto(customer));
    }

    public async Task<Result<CustomerDto>> UpdateAsync(UpdateCustomerRequest request, CancellationToken ct = default)
    {
        var customer = await _customers.GetByIdAsync(request.Id, ct);
        if (customer is null)
            return Result.Failure<CustomerDto>("CUSTOMER_NOT_FOUND", "El cliente no existe.");

        var validation = await ValidateAsync(request.Name, request.RncCedula, request.Id, ct);
        if (validation is not null) return validation;

        customer.Name = request.Name.Trim();
        customer.Phone = request.Phone.Trim();
        customer.RncCedula = request.RncCedula.Trim();
        customer.Email = request.Email.Trim();

        await _customers.UpdateAsync(customer, ct);
        return Result.Success(ToDto(customer));
    }

    public async Task<Result<List<CustomerSaleDto>>> GetHistoryAsync(long customerId, CancellationToken ct = default)
    {
        var customer = await _customers.GetByIdAsync(customerId, ct);
        if (customer is null)
            return Result.Failure<List<CustomerSaleDto>>("CUSTOMER_NOT_FOUND", "El cliente no existe.");

        var sales = await _customers.GetSalesAsync(customerId, ct);
        var dtos = sales
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new CustomerSaleDto
            {
                Number = s.Number,
                CreatedAt = s.CreatedAt,
                Total = s.Total,
                ItemCount = s.Items.Count,
                UserName = s.User?.DisplayName ?? string.Empty,
            })
            .ToList();

        return Result.Success(dtos);
    }

    private async Task<Result<CustomerDto>?> ValidateAsync(string name, string rncCedula, long? excludeId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<CustomerDto>("NAME_REQUIRED", "El nombre del cliente es obligatorio.");

        if (!string.IsNullOrWhiteSpace(rncCedula) && await _customers.RncCedulaExistsAsync(rncCedula.Trim(), excludeId, ct))
            return Result.Failure<CustomerDto>("RNC_DUPLICATED", "Ya existe un cliente con ese RNC o cédula.");

        return null;
    }

    private static CustomerDto ToDto(Customer c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Phone = c.Phone,
        RncCedula = c.RncCedula,
        Email = c.Email,
        CreatedAt = c.CreatedAt,
    };
}