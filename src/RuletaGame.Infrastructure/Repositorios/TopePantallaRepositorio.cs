using Dapper;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Infrastructure.Persistencia;

namespace RuletaGame.Infrastructure.Repositorios;

public sealed class TopePantallaRepositorio(IDbSesion sesion)
    : RepositorioBase(sesion), ITopePantallaRepositorio
{
    private const string Columnas = "PantallaId, Fecha, Monto, Nota, UsuarioId, FechaCreacion";

    public async Task<TopePantalla?> ObtenerAsync(int pantallaId, DateTime fecha) =>
        await Conexion.QuerySingleOrDefaultAsync<TopePantalla>($"""
            SELECT {Columnas} FROM dbo.TopePantalla
            WHERE  PantallaId = @pantallaId AND Fecha = @fecha;
            """, new { pantallaId, fecha = fecha.Date }, Transaccion);

    public async Task<IReadOnlyList<TopePantalla>> ListarDesdeAsync(int pantallaId, DateTime desde)
    {
        var filas = await Conexion.QueryAsync<TopePantalla>($"""
            SELECT {Columnas} FROM dbo.TopePantalla
            WHERE  PantallaId = @pantallaId AND Fecha >= @desde
            ORDER BY Fecha DESC;
            """, new { pantallaId, desde = desde.Date }, Transaccion);

        return filas.ToList();
    }

    public async Task<bool> GuardarAsync(TopePantalla tope) =>
        await Conexion.ExecuteAsync("""
            MERGE dbo.TopePantalla AS destino
            USING (SELECT @PantallaId AS PantallaId, @Fecha AS Fecha) AS origen
                ON destino.PantallaId = origen.PantallaId AND destino.Fecha = origen.Fecha
            WHEN MATCHED THEN
                UPDATE SET Monto = @Monto, Nota = @Nota, UsuarioId = @UsuarioId
            WHEN NOT MATCHED THEN
                INSERT (PantallaId, Fecha, Monto, Nota, UsuarioId)
                VALUES (@PantallaId, @Fecha, @Monto, @Nota, @UsuarioId);
            """, new { tope.PantallaId, Fecha = tope.Fecha.Date, tope.Monto,
                       tope.Nota, tope.UsuarioId }, Transaccion) > 0;

    public async Task<bool> EliminarAsync(int pantallaId, DateTime fecha) =>
        await Conexion.ExecuteAsync("""
            DELETE dbo.TopePantalla WHERE PantallaId = @pantallaId AND Fecha = @fecha;
            """, new { pantallaId, fecha = fecha.Date }, Transaccion) > 0;
}
