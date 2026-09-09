using Dapper;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Infrastructure.Persistencia;

namespace RuletaGame.Infrastructure.Repositorios;

public sealed class EnvioClienteRepositorio(IDbSesion sesion)
    : RepositorioBase(sesion), IEnvioClienteRepositorio
{
    private const string Columnas = """
        e.EnvioId, e.ClienteId, e.JugadaId, e.Estado, e.Intentos, e.ProximoIntento,
        e.PeticionJson, e.RespuestaJson, e.CodigoHttp, e.Mensaje,
        e.FechaCreacion, e.FechaEnvio
        """;

    public async Task<long> EncolarAsync(EnvioCliente envio) =>
        await Conexion.ExecuteScalarAsync<long>("""
            INSERT dbo.EnvioCliente
                (ClienteId, JugadaId, Estado, Intentos, ProximoIntento, PeticionJson)
            VALUES
                (@ClienteId, @JugadaId, @Estado, @Intentos, @ProximoIntento, @PeticionJson);
            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """, envio, Transaccion);

    /// <summary>
    /// Los que toca reintentar. El indice filtrado de la tabla cubre justo
    /// esta consulta, que corre cada pocos segundos.
    /// </summary>
    public async Task<IReadOnlyList<EnvioCliente>> PendientesAsync(int maximo = 20)
    {
        var filas = await Conexion.QueryAsync<EnvioCliente>($"""
            SELECT TOP (@maximo) {Columnas}
            FROM   dbo.EnvioCliente e
            WHERE  e.Estado IN ('PENDIENTE', 'FALLIDO')
              AND  (e.ProximoIntento IS NULL OR e.ProximoIntento <= SYSDATETIME())
            ORDER BY e.EnvioId;
            """, new { maximo }, Transaccion);

        return filas.ToList();
    }

    public async Task<bool> ActualizarAsync(EnvioCliente envio) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.EnvioCliente
            SET    Estado = @Estado,
                   Intentos = @Intentos,
                   ProximoIntento = @ProximoIntento,
                   PeticionJson = @PeticionJson,
                   RespuestaJson = @RespuestaJson,
                   CodigoHttp = @CodigoHttp,
                   Mensaje = @Mensaje,
                   FechaEnvio = @FechaEnvio
            WHERE  EnvioId = @EnvioId;
            """, envio, Transaccion) > 0;

    public async Task<EnvioCliente?> ObtenerAsync(long envioId) =>
        await Conexion.QuerySingleOrDefaultAsync<EnvioCliente>($"""
            SELECT {Columnas} FROM dbo.EnvioCliente e WHERE e.EnvioId = @envioId;
            """, new { envioId }, Transaccion);

    public async Task<IReadOnlyList<EnvioCliente>> ListarAsync(string? estado, int maximo = 200)
    {
        var filas = await Conexion.QueryAsync<EnvioCliente>($"""
            SELECT TOP (@maximo) {Columnas},
                   c.NumeroDocumento,
                   LTRIM(RTRIM(CONCAT(c.ApellidoPaterno, ' ',
                                      ISNULL(c.ApellidoMaterno, ''), ', ',
                                      c.Nombres))) AS NombreCompleto
            FROM   dbo.EnvioCliente e
            JOIN   dbo.Cliente c ON c.ClienteId = e.ClienteId
            WHERE  @estado IS NULL OR e.Estado = @estado
            ORDER BY e.EnvioId DESC;
            """, new { estado, maximo }, Transaccion);

        return filas.ToList();
    }

    public async Task<Dictionary<string, int>> ContarPorEstadoAsync()
    {
        var filas = await Conexion.QueryAsync<(string Estado, int Cuantos)>("""
            SELECT Estado, COUNT(*) AS Cuantos
            FROM   dbo.EnvioCliente
            GROUP BY Estado;
            """, transaction: Transaccion);

        return filas.ToDictionary(f => f.Estado, f => f.Cuantos);
    }
}
