using RuletaGame.Domain.Entities;

namespace RuletaGame.Application.Abstractions;

public interface IPantallaRepositorio
{
    Task<IReadOnlyList<Pantalla>> ListarAsync();
    Task<Pantalla?> ObtenerAsync(int pantallaId);
    Task<Pantalla?> ObtenerPorCodigoAsync(string codigo);
    Task<int>  CrearAsync(Pantalla pantalla);
    Task<bool> ActualizarAsync(Pantalla pantalla);
    Task<bool> AsignarRuletaAsync(int pantallaId, int? ruletaId);
    Task<bool> MarcarConexionAsync(string codigo);

    /// <summary>Solo las terminales web tienen clave.</summary>
    Task<bool> CambiarClaveAsync(int pantallaId, string claveHash);

    Task<bool> CambiarTopeAsync(int pantallaId, decimal? topeMonto);

    /// <summary>
    /// Todas las terminales web activas. Al autenticar hay que probar la clave
    /// contra cada una: el hash impide buscarla directo por igualdad.
    /// </summary>
    Task<IReadOnlyList<Pantalla>> ListarWebActivasAsync();
}
