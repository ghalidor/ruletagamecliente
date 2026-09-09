using Dapper;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Infrastructure.Persistencia;

namespace RuletaGame.Infrastructure.Repositorios;

public sealed class PantallaRepositorio(IDbSesion sesion) : RepositorioBase(sesion), IPantallaRepositorio
{
    private const string Consulta = """
        SELECT  p.PantallaId, p.Codigo, p.Nombre, p.SalaId, p.RuletaAsignadaId,
                p.Tipo, p.ClaveHash, p.TopeMonto,
                p.UltimaConexion, p.Activo, p.FechaCreacion, p.FechaModificacion,
                r.Nombre AS NombreRuleta,
                s.Nombre AS NombreSala
        FROM    dbo.Pantalla p
        LEFT JOIN dbo.Ruleta r ON r.RuletaId = p.RuletaAsignadaId
        LEFT JOIN dbo.Sala   s ON s.SalaId   = p.SalaId
        """;

    public async Task<IReadOnlyList<Pantalla>> ListarAsync()
    {
        var filas = await Conexion.QueryAsync<Pantalla>(
            $"{Consulta} ORDER BY p.Nombre;", transaction: Transaccion);
        return filas.ToList();
    }

    public async Task<Pantalla?> ObtenerAsync(int pantallaId) =>
        await Conexion.QuerySingleOrDefaultAsync<Pantalla>(
            $"{Consulta} WHERE p.PantallaId = @pantallaId;", new { pantallaId }, Transaccion);

    public async Task<Pantalla?> ObtenerPorCodigoAsync(string codigo) =>
        await Conexion.QuerySingleOrDefaultAsync<Pantalla>(
            $"{Consulta} WHERE p.Codigo = @codigo;", new { codigo }, Transaccion);

    public async Task<int> CrearAsync(Pantalla pantalla) =>
        await Conexion.ExecuteScalarAsync<int>("""
            INSERT dbo.Pantalla
                (Codigo, Nombre, SalaId, RuletaAsignadaId, Activo, Tipo, ClaveHash, TopeMonto)
            VALUES
                (@Codigo, @Nombre, @SalaId, @RuletaAsignadaId, @Activo, @Tipo, @ClaveHash, @TopeMonto);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, pantalla, Transaccion);

    public async Task<bool> ActualizarAsync(Pantalla pantalla) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Pantalla
            SET    Codigo = @Codigo, Nombre = @Nombre, SalaId = @SalaId,
                   Activo = @Activo, Tipo = @Tipo, TopeMonto = @TopeMonto,
                   FechaModificacion = SYSDATETIME()
            WHERE  PantallaId = @PantallaId;
            """, pantalla, Transaccion) > 0;

    public async Task<bool> AsignarRuletaAsync(int pantallaId, int? ruletaId) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Pantalla
            SET    RuletaAsignadaId = @ruletaId, FechaModificacion = SYSDATETIME()
            WHERE  PantallaId = @pantallaId;
            """, new { pantallaId, ruletaId }, Transaccion) > 0;

    public async Task<bool> CambiarClaveAsync(int pantallaId, string claveHash) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Pantalla
            SET    ClaveHash = @claveHash, FechaModificacion = SYSDATETIME()
            WHERE  PantallaId = @pantallaId;
            """, new { pantallaId, claveHash }, Transaccion) > 0;

    public async Task<bool> CambiarTopeAsync(int pantallaId, decimal? topeMonto) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Pantalla
            SET    TopeMonto = @topeMonto, FechaModificacion = SYSDATETIME()
            WHERE  PantallaId = @pantallaId;
            """, new { pantallaId, topeMonto }, Transaccion) > 0;

    /// <summary>
    /// Al autenticar hay que probar la clave contra cada terminal web: el hash
    /// impide buscarla directo por igualdad.
    /// </summary>
    public async Task<IReadOnlyList<Pantalla>> ListarWebActivasAsync()
    {
        var filas = await Conexion.QueryAsync<Pantalla>($"""
            {Consulta}
            WHERE p.Tipo = 'WEB' AND p.Activo = 1 AND p.ClaveHash IS NOT NULL
            ORDER BY p.Nombre;
            """, transaction: Transaccion);

        return filas.ToList();
    }

    /// <summary>Latido del kiosco. Alimenta el indicador "en linea" del gestor.</summary>
    public async Task<bool> MarcarConexionAsync(string codigo) =>
        await Conexion.ExecuteAsync(
            "UPDATE dbo.Pantalla SET UltimaConexion = SYSDATETIME() WHERE Codigo = @codigo;",
            new { codigo }, Transaccion) > 0;
}
