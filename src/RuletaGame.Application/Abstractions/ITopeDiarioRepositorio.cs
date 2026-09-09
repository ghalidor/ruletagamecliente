using RuletaGame.Domain.Entities;

namespace RuletaGame.Application.Abstractions;

public interface ITopeDiarioRepositorio
{
    Task<TopeDiario?> ObtenerPorFechaAsync(DateTime fecha);
    Task<IReadOnlyList<TopeDiario>> ListarDesdeAsync(DateTime desde);
    Task<bool> GuardarAsync(TopeDiario tope);
    Task<bool> EliminarAsync(DateTime fecha);
}
