using RuletaGame.Domain.Entities;

namespace RuletaGame.Application.Abstractions;

public interface IRuletaRepositorio
{
    Task<IReadOnlyList<Ruleta>> ListarAsync(bool soloActivas = false);
    Task<Ruleta?> ObtenerAsync(int ruletaId);
    Task<int>  CrearAsync(Ruleta ruleta);
    Task<bool> ActualizarAsync(Ruleta ruleta);
    Task<bool> EliminarAsync(int ruletaId);
    Task<bool> TieneJugadasAsync(int ruletaId);
}
