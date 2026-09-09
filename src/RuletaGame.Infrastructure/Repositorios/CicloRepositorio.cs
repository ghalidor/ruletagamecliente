using Dapper;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Infrastructure.Persistencia;

namespace RuletaGame.Infrastructure.Repositorios;

public sealed class CicloRepositorio(IDbSesion sesion) : RepositorioBase(sesion), ICicloRepositorio
{
    public async Task<Ciclo?> ObtenerAbiertoAsync(int ruletaId) =>
        await Conexion.QuerySingleOrDefaultAsync<Ciclo>("""
            SELECT CicloId, RuletaId, Numero, FechaInicio, FechaCierre, Cerrado
            FROM   dbo.Ciclo
            WHERE  RuletaId = @ruletaId AND Cerrado = 0;
            """, new { ruletaId }, Transaccion);

    public async Task<Ciclo> AbrirAsync(int ruletaId) =>
        await Conexion.QuerySingleAsync<Ciclo>("""
            DECLARE @numero INT =
                (SELECT ISNULL(MAX(Numero), 0) + 1 FROM dbo.Ciclo WHERE RuletaId = @ruletaId);

            INSERT dbo.Ciclo (RuletaId, Numero) VALUES (@ruletaId, @numero);

            SELECT CicloId, RuletaId, Numero, FechaInicio, FechaCierre, Cerrado
            FROM   dbo.Ciclo
            WHERE  CicloId = CAST(SCOPE_IDENTITY() AS INT);
            """, new { ruletaId }, Transaccion);

    public async Task<bool> CerrarAsync(int cicloId) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Ciclo SET Cerrado = 1, FechaCierre = SYSDATETIME()
            WHERE  CicloId = @cicloId AND Cerrado = 0;
            """, new { cicloId }, Transaccion) > 0;

    /// <summary>
    /// Tajadas que ya salieron premiadas Y fueron registradas con DNI dentro
    /// del ciclo. Girar sin registrarse no consume nada.
    /// </summary>
    public async Task<IReadOnlyList<int>> TajadasConsumidasAsync(int cicloId)
    {
        var filas = await Conexion.QueryAsync<int>("""
            SELECT DISTINCT TajadaId
            FROM   dbo.Jugada
            WHERE  CicloId = @cicloId AND ConsumeCiclo = 1;
            """, new { cicloId }, Transaccion);

        return filas.ToList();
    }
}
