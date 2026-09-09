using RuletaGame.Application.Abstractions;
using RuletaGame.Application.Commands;
using RuletaGame.Application.Common;
using RuletaGame.Application.Queries;
using RuletaGame.Application.Servicios;
using RuletaGame.Domain.Enums;
using RuletaGame.Infrastructure.Servicios;

namespace RuletaGame.Web.Api;

public sealed record LoginTerminalRequest(string Clave);
public sealed record GirarWebRequest(int RuletaId, string? VersionRueda);

/// <summary>
/// Terminal web: la ruleta abierta en un navegador, en tablet o celular.
///
/// Separada del pedestal porque la autenticacion es distinta: el pedestal usa
/// un token fijo del archivo de configuracion; la terminal web usa una clave
/// que se escribe una vez y queda en una cookie.
///
/// El juego en si es el MISMO: los dos llaman a GirarCommandHandler. Duplicar
/// esa logica seria condenarse a arreglar cada bug dos veces.
/// </summary>
public static class EndpointsTerminalWeb
{
    /// <summary>Cookie con el codigo de la terminal ya autenticada.</summary>
    public const string CookieTerminal = "s3k_terminal";

    public static void MapearTerminalWeb(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/web").WithTags("TerminalWeb");

        /* ------------------------------------------------------ Acceso --- */

        grupo.MapPost("/acceder", async (
            LoginTerminalRequest peticion,
            TerminalService terminales,
            IConfiguracionRepositorio configuracion,
            HttpContext contexto) =>
        {
            var terminal = await terminales.AutenticarAsync(peticion.Clave);

            if (terminal is null)
                return Results.BadRequest(new { mensaje = "Clave incorrecta." });

            var config = await configuracion.ObtenerDiccionarioAsync();
            int horas = config.LeerEntero("WEB_SESION_HORAS", 12);

            contexto.Response.Cookies.Append(CookieTerminal, terminal.Codigo, new CookieOptions
            {
                HttpOnly = true,
                Secure   = contexto.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires  = DateTimeOffset.Now.AddHours(horas)
            });

            return Results.Ok(new
            {
                terminal.Codigo,
                terminal.Nombre,
                terminal.RuletaAsignadaId
            });
        });

        grupo.MapPost("/salir", (HttpContext contexto) =>
        {
            contexto.Response.Cookies.Delete(CookieTerminal);
            return Results.Ok();
        });

        /* ------------------------------------------------------ Sesion --- */
        // Todo lo de abajo exige la cookie.
        var privado = grupo.MapGroup("").AddEndpointFilter(SesionTerminal);

        /// Estado de la terminal. Es lo primero que pide la pagina al abrir.
        privado.MapGet("/estado", async (HttpContext contexto, IPantallaRepositorio pantallas) =>
        {
            var terminal = await Terminal(contexto, pantallas);

            await pantallas.MarcarConexionAsync(terminal!.Codigo);

            return Results.Ok(new
            {
                terminal.Codigo,
                terminal.Nombre,
                terminal.RuletaAsignadaId
            });
        });

        /// Se pide al desbloquear: asi la rueda siempre arranca actualizada,
        /// que es lo que evita necesitar SignalR en la terminal web.
        privado.MapGet("/ruleta/{ruletaId:int}", async (
            int ruletaId, ObtenerRuletaKioscoQueryHandler handler) =>
        {
            var resultado = await handler.EjecutarAsync(new ObtenerRuletaKioscoQuery(ruletaId));

            return resultado.Exito
                ? Results.Ok(resultado.Valor)
                : Results.NotFound(new { mensaje = resultado.Mensaje });
        });

        privado.MapPost("/girar", async (
            GirarWebRequest peticion,
            HttpContext contexto,
            IPantallaRepositorio pantallas,
            GirarCommandHandler handler) =>
        {
            var terminal = await Terminal(contexto, pantallas);

            var resultado = await handler.EjecutarAsync(
                new GirarCommand(peticion.RuletaId, terminal!.Codigo, peticion.VersionRueda));

            if (resultado.Exito) return Results.Ok(resultado.Valor);

            // 409 y no 400: la terminal sabe que debe recargar la rueda y
            // reintentar, sin mostrarle nada al cliente.
            return resultado.Mensaje == GirarCommandHandler.RECARGAR
                ? Results.Conflict(new { recargar = true })
                : Results.BadRequest(new { mensaje = resultado.Mensaje });
        });

        privado.MapPost("/registrar", async (
            RegistrarClienteRequest peticion, RegistrarClienteCommandHandler handler) =>
        {
            var resultado = await handler.EjecutarAsync(new RegistrarClienteCommand(
                peticion.JugadaId, peticion.TipoDocumento, peticion.NumeroDocumento,
                peticion.Nombres, peticion.ApellidoPaterno, peticion.ApellidoMaterno,
                peticion.Correo, peticion.Telefono,
                peticion.AceptaWhatsapp, peticion.AceptaSms,
                peticion.AceptaLlamada, peticion.AceptaEmail));

            return resultado.Exito
                ? Results.Ok(new { mensaje = resultado.Mensaje })
                : Results.BadRequest(new { mensaje = resultado.Mensaje });
        });


        // Autocompletado desde el padron externo. Solo para DNI: los otros
        // tipos de documento no estan en ese registro.
        //
        // Nunca devuelve error: si el servicio no responde o esta apagado,
        // contesta "no encontrado" y el cliente escribe a mano. El
        // autocompletado es una comodidad, no un requisito.
        privado.MapGet("/padron/{numero}", async (
            string numero,
            IConfiguracionRepositorio configuracion,
            Microsoft.Extensions.Options.IOptions<OpcionesPadron> opciones,
            ServicioPadron padron) =>
        {
            var config = await configuracion.ObtenerDiccionarioAsync();

            if (!config.LeerBool("AUTOCOMPLETAR_DNI", false))
                return Results.Ok(new { encontrado = false, motivo = "deshabilitado" });

            string documento = numero.Trim();

            // Se valida antes de salir a la red: no tiene sentido consultar
            // por algo que ni siquiera tiene forma de DNI.
            if (ReglasDocumento.Validar(TipoDocumento.Dni, documento) is not null)
                return Results.Ok(new { encontrado = false });

            var persona = await padron.ConsultarAsync(
                opciones.Value.Url, documento, opciones.Value.TimeoutSegundos);

            if (persona is null) return Results.Ok(new { encontrado = false });

            return Results.Ok(new
            {
                encontrado = true,
                nombres         = persona.Nombres,
                apellidoPaterno = persona.ApellidoPaterno,
                apellidoMaterno = persona.ApellidoMaterno
            });
        });

        privado.MapGet("/cliente/{tipo:int}/{numero}", async (
            int tipo, string numero, IClienteRepositorio clientes) =>
        {
            if (!Enum.IsDefined(typeof(Domain.Enums.TipoDocumento), (byte)tipo))
                return Results.BadRequest(new { mensaje = "Tipo de documento invalido." });

            var cliente = await clientes.BuscarAsync(
                (Domain.Enums.TipoDocumento)tipo, numero.Trim().ToUpperInvariant());

            if (cliente is null) return Results.Ok(new { encontrado = false });

            return Results.Ok(new
            {
                encontrado = true,
                cliente.Nombres,
                cliente.ApellidoPaterno,
                cliente.ApellidoMaterno,
                cliente.Correo,
                cliente.Telefono,
                cliente.AceptaWhatsapp,
                cliente.AceptaSms,
                cliente.AceptaLlamada,
                cliente.AceptaEmail
            });
        });
    }

    private static async Task<Domain.Entities.Pantalla?> Terminal(
        HttpContext contexto, IPantallaRepositorio pantallas)
    {
        string codigo = contexto.Request.Cookies[CookieTerminal] ?? "";
        return await pantallas.ObtenerPorCodigoAsync(codigo);
    }

    /// <summary>
    /// Exige una cookie valida de terminal web activa. Devuelve 401 para que
    /// la pagina sepa que debe volver a pedir la clave.
    /// </summary>
    private static async ValueTask<object?> SesionTerminal(
        EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        var pantallas = contexto.HttpContext.RequestServices
            .GetRequiredService<IPantallaRepositorio>();

        var terminal = await Terminal(contexto.HttpContext, pantallas);

        if (terminal is null || !terminal.Activo || !terminal.EsWeb)
            return Results.Unauthorized();

        return await siguiente(contexto);
    }
}
