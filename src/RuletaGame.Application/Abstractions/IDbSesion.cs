using System.Data;

namespace RuletaGame.Application.Abstractions;

/// <summary>
/// Conexion compartida por request. Permite que varios repositorios participen
/// de la misma transaccion, que es lo que el sistema legacy no hacia: guardaba
/// el ganador en tres pasos sueltos y podia quedar a medias.
/// </summary>
public interface IDbSesion : IAsyncDisposable
{
    IDbConnection   Conexion    { get; }
    IDbTransaction? Transaccion { get; }

    Task IniciarTransaccionAsync();
    Task ConfirmarAsync();
    Task RevertirAsync();
}
