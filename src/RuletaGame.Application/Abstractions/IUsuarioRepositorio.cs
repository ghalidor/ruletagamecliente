using RuletaGame.Domain.Entities;

namespace RuletaGame.Application.Abstractions;

public interface IUsuarioRepositorio
{
    Task<IReadOnlyList<Usuario>> ListarAsync();
    Task<Usuario?> ObtenerAsync(int usuarioId);
    Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario);
    Task<int>  CrearAsync(Usuario usuario);
    Task<bool> ActualizarAsync(Usuario usuario);
    Task<bool> CambiarPasswordAsync(int usuarioId, string passwordHash);
    Task<bool> RegistrarAccesoAsync(int usuarioId);
    Task<bool> RegistrarFalloAsync(int usuarioId, int intentos, DateTime? bloqueadoHasta);
}
