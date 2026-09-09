using Dapper;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Domain.Enums;
using RuletaGame.Infrastructure.Persistencia;

namespace RuletaGame.Infrastructure.Repositorios;

public sealed class ClienteRepositorio(IDbSesion sesion) : RepositorioBase(sesion), IClienteRepositorio
{
    private const string Columnas = """
        ClienteId, TipoDocumento, NumeroDocumento, Nombres, ApellidoPaterno,
        ApellidoMaterno, Correo, Telefono, AceptaUsoDatos, FechaAceptacion,
        AceptaWhatsapp, AceptaSms, AceptaLlamada, AceptaEmail,
        FechaCreacion, FechaModificacion
        """;

    /// <summary>
    /// La clave natural es el par (tipo, numero): un mismo numero puede existir
    /// como DNI y como carnet sin ser la misma persona.
    /// </summary>
    public async Task<Cliente?> BuscarAsync(TipoDocumento tipo, string numeroDocumento) =>
        await Conexion.QuerySingleOrDefaultAsync<Cliente>($"""
            SELECT {Columnas}
            FROM   dbo.Cliente
            WHERE  TipoDocumento = @tipo AND NumeroDocumento = @numero;
            """, new { tipo = (byte)tipo, numero = numeroDocumento }, Transaccion);

    public async Task<int> CrearAsync(Cliente cliente) =>
        await Conexion.ExecuteScalarAsync<int>("""
            INSERT dbo.Cliente
                (TipoDocumento, NumeroDocumento, Nombres, ApellidoPaterno,
                 ApellidoMaterno, Correo, Telefono, AceptaUsoDatos, FechaAceptacion,
                 AceptaWhatsapp, AceptaSms, AceptaLlamada, AceptaEmail)
            VALUES
                (@TipoDocumento, @NumeroDocumento, @Nombres, @ApellidoPaterno,
                 @ApellidoMaterno, @Correo, @Telefono, @AceptaUsoDatos, @FechaAceptacion,
                 @AceptaWhatsapp, @AceptaSms, @AceptaLlamada, @AceptaEmail);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, cliente, Transaccion);

    public async Task<bool> ActualizarAsync(Cliente cliente) =>
        await Conexion.ExecuteAsync("""
            UPDATE dbo.Cliente
            SET    Nombres = @Nombres,
                   ApellidoPaterno = @ApellidoPaterno,
                   ApellidoMaterno = @ApellidoMaterno,
                   Correo = @Correo,
                   Telefono = @Telefono,
                   AceptaUsoDatos = @AceptaUsoDatos,
                   FechaAceptacion = @FechaAceptacion,
                   AceptaWhatsapp = @AceptaWhatsapp,
                   AceptaSms = @AceptaSms,
                   AceptaLlamada = @AceptaLlamada,
                   AceptaEmail = @AceptaEmail,
                   FechaModificacion = SYSDATETIME()
            WHERE  ClienteId = @ClienteId;
            """, cliente, Transaccion) > 0;

    public async Task<Cliente?> ObtenerAsync(int clienteId) =>
        await Conexion.QuerySingleOrDefaultAsync<Cliente>(
            $"SELECT {Columnas} FROM dbo.Cliente WHERE ClienteId = @clienteId;",
            new { clienteId }, Transaccion);

    public async Task<IReadOnlyList<Cliente>> ListarAsync(string? filtro, int maximo = 200)
    {
        string sql = $"""
            SELECT TOP (@maximo) {Columnas}
            FROM   dbo.Cliente
            WHERE  @filtro IS NULL
               OR  NumeroDocumento LIKE @patron
               OR  Nombres         LIKE @patron
               OR  ApellidoPaterno LIKE @patron
            ORDER BY ApellidoPaterno, Nombres;
            """;

        var filas = await Conexion.QueryAsync<Cliente>(sql, new
        {
            maximo,
            filtro,
            patron = string.IsNullOrWhiteSpace(filtro) ? null : $"%{filtro.Trim()}%"
        }, Transaccion);

        return filas.ToList();
    }
}
