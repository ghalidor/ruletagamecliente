using System.Data;
using Microsoft.Data.SqlClient;
using RuletaGame.Application.Abstractions;

namespace RuletaGame.Infrastructure.Persistencia;

/// <summary>
/// Una conexion por request. Los repositorios la comparten, asi que todos
/// participan de la misma transaccion cuando hay una abierta.
/// </summary>
public sealed class DbSesion : IDbSesion
{
    private readonly SqlConnection _conexion;
    private SqlTransaction? _transaccion;

    public DbSesion(string cadenaConexion)
    {
        _conexion = new SqlConnection(cadenaConexion);
        _conexion.Open();
    }

    public IDbConnection   Conexion    => _conexion;
    public IDbTransaction? Transaccion => _transaccion;

    public Task IniciarTransaccionAsync()
    {
        _transaccion ??= _conexion.BeginTransaction();
        return Task.CompletedTask;
    }

    public Task ConfirmarAsync()
    {
        _transaccion?.Commit();
        _transaccion?.Dispose();
        _transaccion = null;
        return Task.CompletedTask;
    }

    public Task RevertirAsync()
    {
        _transaccion?.Rollback();
        _transaccion?.Dispose();
        _transaccion = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaccion is not null)
        {
            _transaccion.Rollback();   // nadie confirmo: no dejamos a medias
            _transaccion.Dispose();
        }
        await _conexion.DisposeAsync();
    }
}
