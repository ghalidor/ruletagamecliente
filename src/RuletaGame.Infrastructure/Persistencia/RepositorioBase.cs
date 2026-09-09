using System.Data;
using RuletaGame.Application.Abstractions;

namespace RuletaGame.Infrastructure.Persistencia;

/// <summary>
/// Base comun de los repositorios. Todo el SQL de este proyecto va
/// parametrizado; el sistema legacy concatenaba strings y era vulnerable a
/// inyeccion en practicamente cada endpoint.
/// </summary>
public abstract class RepositorioBase(IDbSesion sesion)
{
    protected IDbConnection   Conexion    => sesion.Conexion;
    protected IDbTransaction? Transaccion => sesion.Transaccion;
}
