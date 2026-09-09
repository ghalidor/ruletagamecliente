using Dapper;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Infrastructure.Persistencia;

namespace RuletaGame.Infrastructure.Repositorios;

public sealed class ConfiguracionRepositorio(IDbSesion sesion)
    : RepositorioBase(sesion), IConfiguracionRepositorio
{
    public async Task<IReadOnlyList<Configuracion>> ListarAsync()
    {
        var filas = await Conexion.QueryAsync<Configuracion>("""
            SELECT Clave, Valor, Descripcion, TipoDato, FechaModificacion
            FROM   dbo.Configuracion
            ORDER BY Clave;
            """, transaction: Transaccion);

        return filas.ToList();
    }

    public async Task<Dictionary<string, string>> ObtenerDiccionarioAsync()
    {
        var filas = await Conexion.QueryAsync<(string Clave, string Valor)>(
            "SELECT Clave, Valor FROM dbo.Configuracion;", transaction: Transaccion);

        return filas.ToDictionary(f => f.Clave, f => f.Valor, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> ActualizarAsync(string clave, string valor) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Configuracion
            SET    Valor = @valor, FechaModificacion = SYSDATETIME()
            WHERE  Clave = @clave;
            """, new { clave, valor }, Transaccion) > 0;
}
