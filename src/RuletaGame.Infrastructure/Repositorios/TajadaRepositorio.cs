using Dapper;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Infrastructure.Persistencia;

namespace RuletaGame.Infrastructure.Repositorios;

public sealed class TajadaRepositorio(IDbSesion sesion) : RepositorioBase(sesion), ITajadaRepositorio
{
    private const string Columnas = """
        TajadaId, RuletaId, Descripcion, Tipo, Monto, Probabilidad,
        Imagen, Orden, Activo, AfectaTope, FechaCreacion, FechaModificacion
        """;

    public async Task<IReadOnlyList<Tajada>> ListarPorRuletaAsync(int ruletaId, bool soloActivas = false)
    {
        string sql = $"""
            SELECT {Columnas}
            FROM   dbo.Tajada
            WHERE  RuletaId = @ruletaId
              AND  (@soloActivas = 0 OR Activo = 1)
            ORDER BY Orden, TajadaId;
            """;

        var filas = await Conexion.QueryAsync<Tajada>(sql,
            new { ruletaId, soloActivas = soloActivas ? 1 : 0 }, Transaccion);

        return filas.ToList();
    }

    public async Task<Tajada?> ObtenerAsync(int tajadaId) =>
        await Conexion.QuerySingleOrDefaultAsync<Tajada>(
            $"SELECT {Columnas} FROM dbo.Tajada WHERE TajadaId = @tajadaId;",
            new { tajadaId }, Transaccion);

    public async Task<int> CrearAsync(Tajada tajada) =>
        await Conexion.ExecuteScalarAsync<int>("""
            INSERT dbo.Tajada
                (RuletaId, Descripcion, Tipo, Monto, Probabilidad, Imagen, Orden, Activo, AfectaTope)
            VALUES
                (@RuletaId, @Descripcion, @Tipo, @Monto, @Probabilidad, @Imagen, @Orden, @Activo, @AfectaTope);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, tajada, Transaccion);

    public async Task<bool> ActualizarAsync(Tajada tajada) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Tajada
            SET    Descripcion = @Descripcion, Tipo = @Tipo, Monto = @Monto,
                   Probabilidad = @Probabilidad, Imagen = @Imagen, Orden = @Orden,
                   Activo = @Activo, AfectaTope = @AfectaTope,
                   FechaModificacion = SYSDATETIME()
            WHERE  TajadaId = @TajadaId;
            """, tajada, Transaccion) > 0;

    public async Task<bool> EliminarAsync(int tajadaId) =>
        await Conexion.ExecuteAsync("DELETE dbo.Tajada WHERE TajadaId = @tajadaId;",
            new { tajadaId }, Transaccion) > 0;

    public async Task<bool> CambiarActivoAsync(int tajadaId, bool activo) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Tajada SET Activo = @activo, FechaModificacion = SYSDATETIME()
            WHERE TajadaId = @tajadaId;
            """, new { tajadaId, activo }, Transaccion) > 0;

    public async Task<bool> ActualizarProbabilidadAsync(int tajadaId, decimal? probabilidad) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Tajada SET Probabilidad = @probabilidad, FechaModificacion = SYSDATETIME()
            WHERE TajadaId = @tajadaId;
            """, new { tajadaId, probabilidad }, Transaccion) > 0;

    /// <summary>
    /// Suma de probabilidades de la ruleta. El gestor valida contra esto en el
    /// servidor: en el legacy la suma menor o igual a 100% solo se revisaba en
    /// el navegador y unicamente al presionar Enter.
    /// </summary>
    public async Task<decimal> SumaProbabilidadesAsync(int ruletaId, int? excluirTajadaId = null) =>
        await Conexion.ExecuteScalarAsync<decimal?>("""
            SELECT ISNULL(SUM(Probabilidad), 0)
            FROM   dbo.Tajada
            WHERE  RuletaId = @ruletaId AND Activo = 1
              AND  (@excluir IS NULL OR TajadaId <> @excluir);
            """, new { ruletaId, excluir = excluirTajadaId }, Transaccion) ?? 0m;

    public async Task<int> SiguienteOrdenAsync(int ruletaId) =>
        await Conexion.ExecuteScalarAsync<int>(
            "SELECT ISNULL(MAX(Orden), 0) + 1 FROM dbo.Tajada WHERE RuletaId = @ruletaId;",
            new { ruletaId }, Transaccion);
}
