using RuletaGame.Domain.Entities;

namespace RuletaGame.Application.Abstractions;

public interface IEnvioClienteRepositorio
{
    Task<long> EncolarAsync(EnvioCliente envio);

    /// <summary>Envios que toca reintentar ahora.</summary>
    Task<IReadOnlyList<EnvioCliente>> PendientesAsync(int maximo = 20);

    Task<bool> ActualizarAsync(EnvioCliente envio);

    /// <summary>Para la pantalla de seguimiento del gestor.</summary>
    Task<IReadOnlyList<EnvioCliente>> ListarAsync(string? estado, int maximo = 200);

    Task<EnvioCliente?> ObtenerAsync(long envioId);

    /// <summary>Cuantos hay en cada estado. Alimenta el resumen del tablero.</summary>
    Task<Dictionary<string, int>> ContarPorEstadoAsync();
}
