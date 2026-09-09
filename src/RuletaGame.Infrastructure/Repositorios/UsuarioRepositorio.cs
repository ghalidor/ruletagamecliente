using Dapper;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Infrastructure.Persistencia;

namespace RuletaGame.Infrastructure.Repositorios;

public sealed class UsuarioRepositorio(IDbSesion sesion) : RepositorioBase(sesion), IUsuarioRepositorio
{
    private const string Columnas = """
        UsuarioId, NombreUsuario, NombreCompleto, PasswordHash, Rol, Activo,
        DebeCambiarPassword, UltimoAcceso, IntentosFallidos, BloqueadoHasta,
        FechaCreacion, FechaModificacion
        """;

    public async Task<IReadOnlyList<Usuario>> ListarAsync()
    {
        var filas = await Conexion.QueryAsync<Usuario>(
            $"SELECT {Columnas} FROM dbo.Usuario ORDER BY NombreUsuario;", transaction: Transaccion);
        return filas.ToList();
    }

    public async Task<Usuario?> ObtenerAsync(int usuarioId) =>
        await Conexion.QuerySingleOrDefaultAsync<Usuario>(
            $"SELECT {Columnas} FROM dbo.Usuario WHERE UsuarioId = @usuarioId;",
            new { usuarioId }, Transaccion);

    public async Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario) =>
        await Conexion.QuerySingleOrDefaultAsync<Usuario>(
            $"SELECT {Columnas} FROM dbo.Usuario WHERE NombreUsuario = @nombreUsuario;",
            new { nombreUsuario }, Transaccion);

    public async Task<int> CrearAsync(Usuario usuario) =>
        await Conexion.ExecuteScalarAsync<int>("""
            INSERT dbo.Usuario
                (NombreUsuario, NombreCompleto, PasswordHash, Rol, Activo, DebeCambiarPassword)
            VALUES
                (@NombreUsuario, @NombreCompleto, @PasswordHash, @Rol, @Activo, @DebeCambiarPassword);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, usuario, Transaccion);

    public async Task<bool> ActualizarAsync(Usuario usuario) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Usuario
            SET    NombreCompleto = @NombreCompleto, Rol = @Rol, Activo = @Activo,
                   FechaModificacion = SYSDATETIME()
            WHERE  UsuarioId = @UsuarioId;
            """, usuario, Transaccion) > 0;

    public async Task<bool> CambiarPasswordAsync(int usuarioId, string passwordHash) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Usuario
            SET    PasswordHash = @passwordHash, DebeCambiarPassword = 0,
                   FechaModificacion = SYSDATETIME()
            WHERE  UsuarioId = @usuarioId;
            """, new { usuarioId, passwordHash }, Transaccion) > 0;

    public async Task<bool> RegistrarAccesoAsync(int usuarioId) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Usuario
            SET    UltimoAcceso = SYSDATETIME(), IntentosFallidos = 0, BloqueadoHasta = NULL
            WHERE  UsuarioId = @usuarioId;
            """, new { usuarioId }, Transaccion) > 0;

    public async Task<bool> RegistrarFalloAsync(int usuarioId, int intentos, DateTime? bloqueadoHasta) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Usuario
            SET    IntentosFallidos = @intentos, BloqueadoHasta = @bloqueadoHasta
            WHERE  UsuarioId = @usuarioId;
            """, new { usuarioId, intentos, bloqueadoHasta }, Transaccion) > 0;
}
