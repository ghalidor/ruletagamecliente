using RuletaGame.Domain.Entities;
using RuletaGame.Domain.Enums;

namespace RuletaGame.Application.Abstractions;

public interface IClienteRepositorio
{
    Task<Cliente?> BuscarAsync(TipoDocumento tipo, string numeroDocumento);
    Task<Cliente?> ObtenerAsync(int clienteId);
    Task<int>  CrearAsync(Cliente cliente);
    Task<bool> ActualizarAsync(Cliente cliente);
    Task<IReadOnlyList<Cliente>> ListarAsync(string? filtro, int maximo = 200);
}
