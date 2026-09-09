using Dapper;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Infrastructure.Persistencia;

namespace RuletaGame.Infrastructure.Repositorios;

public sealed class RuletaRepositorio(IDbSesion sesion) : RepositorioBase(sesion), IRuletaRepositorio
{
    public async Task<IReadOnlyList<Ruleta>> ListarAsync(bool soloActivas = false)
    {
        const string sql = """
            SELECT  r.RuletaId, r.Nombre, r.Descripcion, r.Activo,
                    r.UsuarioCreacionId, r.FechaCreacion, r.FechaModificacion,
                    COUNT(t.TajadaId)                              AS TotalTajadas,
                    SUM(CASE WHEN t.Activo = 1 THEN 1 ELSE 0 END)  AS TajadasActivas
            FROM    dbo.Ruleta r
            LEFT JOIN dbo.Tajada t ON t.RuletaId = r.RuletaId
            WHERE   (@soloActivas = 0 OR r.Activo = 1)
            GROUP BY r.RuletaId, r.Nombre, r.Descripcion, r.Activo,
                     r.UsuarioCreacionId, r.FechaCreacion, r.FechaModificacion
            ORDER BY r.Nombre;
            """;

        var filas = await Conexion.QueryAsync<Ruleta>(sql,
            new { soloActivas = soloActivas ? 1 : 0 }, Transaccion);

        return filas.ToList();
    }

    public async Task<Ruleta?> ObtenerAsync(int ruletaId)
    {
        const string sql = """
            SELECT  r.RuletaId, r.Nombre, r.Descripcion, r.Activo,
                    r.UsuarioCreacionId, r.FechaCreacion, r.FechaModificacion,
                    COUNT(t.TajadaId)                              AS TotalTajadas,
                    SUM(CASE WHEN t.Activo = 1 THEN 1 ELSE 0 END)  AS TajadasActivas
            FROM    dbo.Ruleta r
            LEFT JOIN dbo.Tajada t ON t.RuletaId = r.RuletaId
            WHERE   r.RuletaId = @ruletaId
            GROUP BY r.RuletaId, r.Nombre, r.Descripcion, r.Activo,
                     r.UsuarioCreacionId, r.FechaCreacion, r.FechaModificacion;
            """;

        return await Conexion.QuerySingleOrDefaultAsync<Ruleta>(sql, new { ruletaId }, Transaccion);
    }

    public async Task<int> CrearAsync(Ruleta ruleta) =>
        await Conexion.ExecuteScalarAsync<int>("""
            INSERT dbo.Ruleta (Nombre, Descripcion, Activo, UsuarioCreacionId)
            VALUES (@Nombre, @Descripcion, @Activo, @UsuarioCreacionId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, ruleta, Transaccion);

    public async Task<bool> ActualizarAsync(Ruleta ruleta) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Ruleta
            SET    Nombre = @Nombre, Descripcion = @Descripcion, Activo = @Activo,
                   FechaModificacion = SYSDATETIME()
            WHERE  RuletaId = @RuletaId;
            """, ruleta, Transaccion) > 0;

    /// <summary>
    /// Borra la ruleta y sus tajadas. El que llama debe validar antes con
    /// TieneJugadasAsync: el historico no se toca.
    /// </summary>
    public async Task<bool> EliminarAsync(int ruletaId) =>
        await Conexion.ExecuteAsync("""
            DELETE dbo.Tajada WHERE RuletaId = @ruletaId;
            DELETE dbo.Ruleta WHERE RuletaId = @ruletaId;
            """, new { ruletaId }, Transaccion) > 0;

    public async Task<bool> TieneJugadasAsync(int ruletaId) =>
        await Conexion.ExecuteScalarAsync<int?>(
            "SELECT TOP 1 1 FROM dbo.Jugada WHERE RuletaId = @ruletaId;",
            new { ruletaId }, Transaccion) is not null;
}
