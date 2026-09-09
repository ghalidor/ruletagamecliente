using System.Text.RegularExpressions;
using RuletaGame.Application.Abstractions;
using RuletaGame.Application.Common;
using RuletaGame.Domain.Entities;
using RuletaGame.Domain.Enums;

namespace RuletaGame.Application.Commands;

public sealed record RegistrarClienteCommand(
    long   JugadaId,
    int    TipoDocumento,
    string NumeroDocumento,
    string Nombres,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string? Correo,
    string? Telefono,

    // Canales autorizados. Si los cuatro van en false, el cliente marco
    // "No autorizo" y no se le puede contactar.
    bool   AceptaWhatsapp,
    bool   AceptaSms,
    bool   AceptaLlamada,
    bool   AceptaEmail);

/// <summary>
/// Registra al cliente que gano y lo asocia a la jugada.
///
/// Solo aca la tajada consume el ciclo: si el cliente toca "Continuar" sin
/// identificarse, el premio sigue disponible para el siguiente giro.
///
/// Si el documento ya existe, se actualizan sus datos en vez de duplicarlo:
/// la misma persona puede jugar muchas veces y sus datos deben ser uno solo.
/// </summary>
public sealed class RegistrarClienteCommandHandler(
    IJugadaRepositorio        jugadas,
    IClienteRepositorio       clientes,
    IEnvioClienteRepositorio  envios,
    ICicloRepositorio         ciclos,
    ITajadaRepositorio        tajadas,
    IConfiguracionRepositorio configuracion,
    IDbSesion                 sesion)
{
    private static readonly Regex FormatoCorreo =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);

    public async Task<Resultado> EjecutarAsync(RegistrarClienteCommand comando)
    {
        var config = await configuracion.ObtenerDiccionarioAsync();

        var validacion = Validar(comando, config);
        if (validacion is not null) return Resultado.Fallo(validacion);

        var jugada = await jugadas.ObtenerAsync(comando.JugadaId);
        if (jugada is null)    return Resultado.Fallo("La jugada no existe.");
        if (jugada.Registrado) return Resultado.Fallo("Esta jugada ya tiene un cliente registrado.");

        var tipo    = (TipoDocumento)comando.TipoDocumento;
        string numero = comando.NumeroDocumento.Trim().ToUpperInvariant();

        bool cicloHabilitado = config.LeerBool(ClaveConfiguracion.CicloHabilitado, true);

        await sesion.IniciarTransaccionAsync();
        try
        {
            int clienteId = await GuardarClienteAsync(comando, tipo, numero);

            await jugadas.RegistrarClienteAsync(
                comando.JugadaId, clienteId, comando.TipoDocumento, numero, cicloHabilitado);

            // Si con esta jugada se agotaron las tajadas, el ciclo se cierra y
            // el siguiente giro arranca uno nuevo.
            if (cicloHabilitado && jugada.CicloId.HasValue)
            {
                var activas    = await tajadas.ListarPorRuletaAsync(jugada.RuletaId, soloActivas: true);
                var consumidas = await ciclos.TajadasConsumidasAsync(jugada.CicloId.Value);

                if (activas.All(t => consumidas.Contains(t.TajadaId)))
                    await ciclos.CerrarAsync(jugada.CicloId.Value);
            }

            // El envio al sistema externo se ENCOLA, no se hace aqui: el
            // cliente esta parado frente al pedestal y no puede esperar a que
            // un servicio de terceros conteste. Un proceso de fondo lo manda.
            if (config.LeerBool("ENVIAR_CLIENTE_EXTERNO", false))
            {
                await envios.EncolarAsync(new EnvioCliente
                {
                    ClienteId      = clienteId,
                    JugadaId       = comando.JugadaId,
                    Estado         = EstadoEnvio.Pendiente,
                    ProximoIntento = DateTime.Now
                });
            }

            await sesion.ConfirmarAsync();
            return Resultado.Ok("Datos registrados correctamente.");
        }
        catch
        {
            await sesion.RevertirAsync();
            throw;
        }
    }

    /// <summary>Crea el cliente o actualiza el existente. Devuelve su id.</summary>
    private async Task<int> GuardarClienteAsync(
        RegistrarClienteCommand comando, TipoDocumento tipo, string numero)
    {
        var existente = await clientes.BuscarAsync(tipo, numero);

        string? correo   = Limpiar(comando.Correo)?.ToLowerInvariant();
        string? telefono = Limpiar(comando.Telefono);

        if (existente is not null)
        {
            existente.Nombres         = comando.Nombres.Trim();
            existente.ApellidoPaterno = comando.ApellidoPaterno.Trim();
            existente.ApellidoMaterno = Limpiar(comando.ApellidoMaterno);

            // Un dato que ya teniamos no se borra porque esta vez lo dejaron
            // vacio: solo se reemplaza si viene algo nuevo.
            existente.Correo   = correo   ?? existente.Correo;
            existente.Telefono = telefono ?? existente.Telefono;

            // Los canales se reemplazan por lo elegido ahora: si el cliente
            // decide hoy que no quiere WhatsApp, esa es su voluntad vigente.
            existente.AceptaWhatsapp = comando.AceptaWhatsapp;
            existente.AceptaSms      = comando.AceptaSms;
            existente.AceptaLlamada  = comando.AceptaLlamada;
            existente.AceptaEmail    = comando.AceptaEmail;

            bool autorizaAlgo = comando.AceptaWhatsapp || comando.AceptaSms
                             || comando.AceptaLlamada  || comando.AceptaEmail;

            existente.AceptaUsoDatos = autorizaAlgo;

            if (autorizaAlgo && existente.FechaAceptacion is null)
                existente.FechaAceptacion = DateTime.Now;

            await clientes.ActualizarAsync(existente);
            return existente.ClienteId;
        }

        return await clientes.CrearAsync(new Cliente
        {
            TipoDocumento   = tipo,
            NumeroDocumento = numero,
            Nombres         = comando.Nombres.Trim(),
            ApellidoPaterno = comando.ApellidoPaterno.Trim(),
            ApellidoMaterno = Limpiar(comando.ApellidoMaterno),
            Correo          = correo,
            Telefono        = telefono,

            AceptaWhatsapp  = comando.AceptaWhatsapp,
            AceptaSms       = comando.AceptaSms,
            AceptaLlamada   = comando.AceptaLlamada,
            AceptaEmail     = comando.AceptaEmail,

            AceptaUsoDatos  = AutorizaAlgo(comando),
            FechaAceptacion = AutorizaAlgo(comando) ? DateTime.Now : null
        });
    }

    private static string? Validar(
        RegistrarClienteCommand comando, Dictionary<string, string> config)
    {
        if (!Enum.IsDefined(typeof(TipoDocumento), (byte)comando.TipoDocumento))
            return "Tipo de documento invalido.";

        var tipo = (TipoDocumento)comando.TipoDocumento;

        string? errorDocumento = ReglasDocumento.Validar(tipo, comando.NumeroDocumento);
        if (errorDocumento is not null) return errorDocumento;

        if (string.IsNullOrWhiteSpace(comando.Nombres))
            return "Escribe los nombres.";

        if (string.IsNullOrWhiteSpace(comando.ApellidoPaterno))
            return "Escribe el apellido paterno.";

        string? correo = Limpiar(comando.Correo);
        bool correoObligatorio = config.LeerBool("CLIENTE_CORREO_OBLIGATORIO", false);

        if (correoObligatorio && correo is null)
            return "El correo es obligatorio.";

        if (correo is not null && !FormatoCorreo.IsMatch(correo))
            return "El correo no tiene un formato valido.";

        string? telefono = Limpiar(comando.Telefono);
        bool telefonoObligatorio = config.LeerBool("CLIENTE_TELEFONO_OBLIGATORIO", false);

        if (telefonoObligatorio && telefono is null)
            return "El telefono es obligatorio.";

        if (telefono is not null && (telefono.Length < 6 || telefono.Length > 15
                                     || !telefono.All(c => char.IsDigit(c) || c == '+')))
            return "El telefono no es valido.";

        // "No autorizo" es una respuesta valida: el cliente igual se registra
        // para recibir su premio, solo que no se le puede contactar despues.
        return null;
    }

    private static bool AutorizaAlgo(RegistrarClienteCommand c) =>
        c.AceptaWhatsapp || c.AceptaSms || c.AceptaLlamada || c.AceptaEmail;

    private static string? Limpiar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
