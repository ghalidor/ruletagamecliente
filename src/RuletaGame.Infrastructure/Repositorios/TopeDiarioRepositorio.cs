using Dapper;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Infrastructure.Persistencia;

namespace RuletaGame.Infrastructure.Repositorios;

public sealed class TopeDiarioRepositorio(IDbSesion sesion)
    : RepositorioBase(sesion), ITopeDiarioRepositorio
{
    public async Task<TopeDiario?> ObtenerPorFechaAsync(DateTime fecha) =>
        await Conexion.QuerySingleOrDefaultAsync<TopeDiario>("""
            SELECT Fecha, Monto, Nota, UsuarioId, FechaCreacion
            FROM   dbo.TopeDiario WHERE Fecha = @fecha;
            """, new { fecha = fecha.Date }, Transaccion);

    public async Task<IReadOnlyList<TopeDiario>> ListarDesdeAsync(DateTime desde)
    {
        var filas = await Conexion.QueryAsync<TopeDiario>("""
            SELECT Fecha, Monto, Nota, UsuarioId, FechaCreacion
            FROM   dbo.TopeDiario
            WHERE  Fecha >= @desde
            ORDER BY Fecha DESC;
            """, new { desde = desde.Date }, Transaccion);

        return filas.ToList();
    }

    /// <summary>Inserta o reemplaza la excepcion de esa fecha.</summary>
    public async Task<bool> GuardarAsync(TopeDiario tope) =>
        await Conexion.ExecuteAsync("""
            MERGE dbo.TopeDiario AS destino
            USING (SELECT @Fecha AS Fecha) AS origen
                ON destino.Fecha = origen.Fecha
            WHEN MATCHED THEN
                UPDATE SET Monto = @Monto, Nota = @Nota, UsuarioId = @UsuarioId
            WHEN NOT MATCHED THEN
                INSERT (Fecha, Monto, Nota, UsuarioId)
                VALUES (@Fecha, @Monto, @Nota, @UsuarioId);
            """, new { Fecha = tope.Fecha.Date, tope.Monto, tope.Nota, tope.UsuarioId },
            Transaccion) > 0;

    public async Task<bool> EliminarAsync(DateTime fecha) =>
        await Conexion.ExecuteAsync("DELETE dbo.TopeDiario WHERE Fecha = @fecha;",
            new { fecha = fecha.Date }, Transaccion) > 0;
}
