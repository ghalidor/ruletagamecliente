using RuletaGame.Domain.Entities;

namespace RuletaGame.Application.Abstractions;

public interface ICicloRepositorio
{
    Task<Ciclo?> ObtenerAbiertoAsync(int ruletaId);
    Task<Ciclo>  AbrirAsync(int ruletaId);
    Task<bool>   CerrarAsync(int cicloId);

    /// <summary>Ids de tajada ya premiadas y registradas dentro del ciclo.</summary>
    Task<IReadOnlyList<int>> TajadasConsumidasAsync(int cicloId);
}
