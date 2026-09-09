using RuletaGame.Domain.Entities;

namespace RuletaGame.Application.Abstractions;

public interface IJugadaRepositorio
{
    Task<long> CrearAsync(Jugada jugada);
    Task<Jugada?> ObtenerAsync(long jugadaId);
    Task<bool> RegistrarClienteAsync(
        long jugadaId, int clienteId, int tipoDocumento, string numeroDocumento, bool consumeCiclo);
    Task<IReadOnlyList<Jugada>> ReporteAsync(
        DateTime desde, DateTime hasta, int? ruletaId, bool soloRegistradas);

    /// <summary>
    /// Monto que descuenta del tope hoy. GLOBAL: suma todas las ruletas, igual
    /// que el sistema legacy. Solo cuenta las tajadas con AfectaTope = 1.
    /// </summary>
    Task<decimal> MontoQueDescuentaHoyAsync();

    /// <summary>
    /// Igual que el anterior pero acotado a una terminal. Se usa cuando la
    /// terminal tiene tope propio: ahi solo cuenta lo que ella entrego.
    /// </summary>
    Task<decimal> MontoQueDescuentaHoyPorPantallaAsync(int pantallaId);

    /// <summary>Total entregado hoy, descuente o no. Para el reporte.</summary>
    Task<decimal> MontoEntregadoHoyAsync(int? ruletaId = null);

    Task<int> ContarHoyAsync(int? ruletaId);

    /// <summary>
    /// Tajada del ultimo giro de esta ruleta, sin importar si se registro.
    /// Se usa para no repetir el mismo premio dos veces seguidas.
    /// </summary>
    Task<int?> UltimaTajadaAsync(int ruletaId, int? pantallaId);
}
