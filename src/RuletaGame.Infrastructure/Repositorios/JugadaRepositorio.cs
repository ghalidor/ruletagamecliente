using Dapper;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Infrastructure.Persistencia;

namespace RuletaGame.Infrastructure.Repositorios;

public sealed class JugadaRepositorio(IDbSesion sesion) : RepositorioBase(sesion), IJugadaRepositorio
{
    public async Task<long> CrearAsync(Jugada jugada) =>
        await Conexion.ExecuteScalarAsync<long>("""
            INSERT dbo.Jugada
            (
                RuletaId, TajadaId, CicloId, PantallaId,
                DescripcionPremio, TipoPremio, MontoPremio, AfectaTope,
                ProbabilidadAplicada, MontoTopeVigente,
                AnguloFinal, SorteoPonderado, CandidatasEnSorteo,
                Dni, Registrado, ConsumeCiclo, FechaJuego
            )
            VALUES
            (
                @RuletaId, @TajadaId, @CicloId, @PantallaId,
                @DescripcionPremio, @TipoPremio, @MontoPremio, @AfectaTope,
                @ProbabilidadAplicada, @MontoTopeVigente,
                @AnguloFinal, @SorteoPonderado, @CandidatasEnSorteo,
                @Dni, @Registrado, @ConsumeCiclo, @FechaJuego
            );
            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """, jugada, Transaccion);

    public async Task<Jugada?> ObtenerAsync(long jugadaId) =>
        await Conexion.QuerySingleOrDefaultAsync<Jugada>("""
            SELECT JugadaId, RuletaId, TajadaId, CicloId, PantallaId,
                   DescripcionPremio, TipoPremio, MontoPremio, AfectaTope, ProbabilidadAplicada,
                   MontoTopeVigente, AnguloFinal, SorteoPonderado, CandidatasEnSorteo,
                   Dni, Registrado, ConsumeCiclo, FechaJuego, FechaRegistro
            FROM   dbo.Jugada
            WHERE  JugadaId = @jugadaId;
            """, new { jugadaId }, Transaccion);

    /// <summary>
    /// El filtro Registrado = 0 evita que un doble envio pise un DNI ya
    /// guardado o vuelva a consumir el ciclo.
    /// </summary>
    public async Task<bool> RegistrarClienteAsync(
        long jugadaId, int clienteId, int tipoDocumento, string numeroDocumento, bool consumeCiclo) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Jugada
            SET    ClienteId = @clienteId,
                   TipoDocumento = @tipoDocumento,
                   Dni = @numeroDocumento,
                   Registrado = 1,
                   ConsumeCiclo = @consumeCiclo,
                   FechaRegistro = SYSDATETIME()
            WHERE  JugadaId = @jugadaId AND Registrado = 0;
            """, new { jugadaId, clienteId, tipoDocumento, numeroDocumento, consumeCiclo },
            Transaccion) > 0;

    public async Task<IReadOnlyList<Jugada>> ReporteAsync(
        DateTime desde, DateTime hasta, int? ruletaId, bool soloRegistradas)
    {
        const string sql = """
            SELECT  j.JugadaId, j.RuletaId, j.TajadaId, j.CicloId, j.PantallaId,
                    j.DescripcionPremio, j.TipoPremio, j.MontoPremio, j.AfectaTope,
                    j.Dni, j.TipoDocumento, j.ClienteId, j.Registrado, j.ConsumeCiclo,
                    j.FechaJuego, j.FechaRegistro,
                    r.Nombre AS NombreRuleta,
                    p.Nombre AS NombrePantalla,
                    LTRIM(RTRIM(CONCAT(c.ApellidoPaterno, ' ',
                                       ISNULL(c.ApellidoMaterno, ''), ', ',
                                       c.Nombres)))  AS NombreCliente,
                    c.Correo AS CorreoCliente
            FROM    dbo.Jugada j
            JOIN    dbo.Ruleta r   ON r.RuletaId   = j.RuletaId
            LEFT JOIN dbo.Pantalla p ON p.PantallaId = j.PantallaId
            LEFT JOIN dbo.Cliente  c ON c.ClienteId  = j.ClienteId
            WHERE   j.FechaJuego >= @desde
              AND   j.FechaJuego <  @hasta
              AND   (@ruletaId IS NULL OR j.RuletaId = @ruletaId)
              AND   (@soloRegistradas = 0 OR j.Registrado = 1)
            ORDER BY j.FechaJuego DESC;
            """;

        var filas = await Conexion.QueryAsync<Jugada>(sql, new
        {
            // Ambos extremos normalizados: si llegara una fecha con hora, el
            // primer dia perderia las jugadas de la manana.
            desde = desde.Date,
            hasta = hasta.Date.AddDays(1),   // rango inclusivo en el dia final
            ruletaId,
            soloRegistradas = soloRegistradas ? 1 : 0
        }, Transaccion);

        return filas.ToList();
    }

    /// <summary>
    /// Lo que descuenta del tope hoy. Sin filtro de ruleta: el tope es global,
    /// tal como estaba en el legacy.
    ///
    /// Lee AfectaTope de la JUGADA, no de la tajada: asi cambiar hoy la marca
    /// de un premio no altera el consumo de dias pasados.
    /// </summary>
    public async Task<decimal> MontoQueDescuentaHoyAsync() =>
        await Conexion.ExecuteScalarAsync<decimal?>("""
            SELECT ISNULL(SUM(MontoPremio), 0)
            FROM   dbo.Jugada
            WHERE  CAST(FechaJuego AS DATE) = CAST(SYSDATETIME() AS DATE)
              AND  AfectaTope = 1;
            """, transaction: Transaccion) ?? 0m;

    /// <summary>
    /// Consumo de una sola terminal. Cuando tiene tope propio, lo que hagan
    /// las demas no le afecta: el tope individual reemplaza al general.
    /// </summary>
    public async Task<decimal> MontoQueDescuentaHoyPorPantallaAsync(int pantallaId) =>
        await Conexion.ExecuteScalarAsync<decimal?>("""
            SELECT ISNULL(SUM(MontoPremio), 0)
            FROM   dbo.Jugada
            WHERE  PantallaId = @pantallaId
              AND  CAST(FechaJuego AS DATE) = CAST(SYSDATETIME() AS DATE)
              AND  AfectaTope = 1;
            """, new { pantallaId }, Transaccion) ?? 0m;

    public async Task<decimal> MontoEntregadoHoyAsync(int? ruletaId = null) =>
        await Conexion.ExecuteScalarAsync<decimal?>("""
            SELECT ISNULL(SUM(MontoPremio), 0)
            FROM   dbo.Jugada
            WHERE  CAST(FechaJuego AS DATE) = CAST(SYSDATETIME() AS DATE)
              AND  (@ruletaId IS NULL OR RuletaId = @ruletaId);
            """, new { ruletaId }, Transaccion) ?? 0m;

    /// <summary>
    /// Ultimo premio entregado por esta ruleta. Se acota a la terminal cuando
    /// se indica: con varios pedestales mostrando la misma ruleta, lo que
    /// importa es lo que vio el publico de ESA pantalla.
    /// </summary>
    public async Task<int?> UltimaTajadaAsync(int ruletaId, int? pantallaId) =>
        await Conexion.ExecuteScalarAsync<int?>("""
            SELECT TOP 1 TajadaId
            FROM   dbo.Jugada
            WHERE  RuletaId = @ruletaId
              AND  (@pantallaId IS NULL OR PantallaId = @pantallaId)
            ORDER BY JugadaId DESC;
            """, new { ruletaId, pantallaId }, Transaccion);

    public async Task<int> ContarHoyAsync(int? ruletaId) =>
        await Conexion.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)
            FROM   dbo.Jugada
            WHERE  CAST(FechaJuego AS DATE) = CAST(SYSDATETIME() AS DATE)
              AND  (@ruletaId IS NULL OR RuletaId = @ruletaId);
            """, new { ruletaId }, Transaccion);
}
