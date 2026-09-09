using RuletaGame.Domain.Entities;

namespace RuletaGame.Application.Abstractions;

public interface IConfiguracionRepositorio
{
    Task<IReadOnlyList<Configuracion>> ListarAsync();
    Task<Dictionary<string, string>> ObtenerDiccionarioAsync();
    Task<bool> ActualizarAsync(string clave, string valor);
}
