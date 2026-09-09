using RuletaGame.Domain.Entities;

namespace RuletaGame.Application.Abstractions;

public interface ITopePantallaRepositorio
{
    Task<TopePantalla?> ObtenerAsync(int pantallaId, DateTime fecha);
    Task<IReadOnlyList<TopePantalla>> ListarDesdeAsync(int pantallaId, DateTime desde);
    Task<bool> GuardarAsync(TopePantalla tope);
    Task<bool> EliminarAsync(int pantallaId, DateTime fecha);
}
