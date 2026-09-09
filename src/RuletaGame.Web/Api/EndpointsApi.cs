using RuletaGame.Application.Abstractions;
using RuletaGame.Application.Commands;
using RuletaGame.Application.Queries;
using RuletaGame.Application.Common;
using RuletaGame.Domain.Enums;
using RuletaGame.Infrastructure.Servicios;

namespace RuletaGame.Web.Api;

public static class EndpointsApi
{
    /// <summary>
    /// Cookie donde viaja el JWT. Es HttpOnly, asi que el JavaScript de la
    /// pagina no puede leerla: eso cierra la puerta a que un XSS se robe el
    /// token, que es el problema de guardarlo en localStorage.
    /// </summary>
    public const string CookieToken = "s3k_token";

    public static void MapearEndpoints(this WebApplication app)
    {
        MapearAuth(app);
        MapearKiosco(app);
        MapearGestion(app);
    }

    // ---------------------------------------------------------------- Auth
    private static void MapearAuth(WebApplication app)
    {
        var grupo = app.MapGroup("/api/auth").WithTags("Auth");

        grupo.MapPost("/login", async (
            LoginRequest peticion,
            LoginCommandHandler handler,
            HttpContext contexto) =>
        {
            var resultado = await handler.EjecutarAsync(
                new LoginCommand(peticion.Usuario, peticion.Password));

            if (!resultado.Exito)
                return Results.BadRequest(new { mensaje = resultado.Mensaje });

            var datos = resultado.Valor!;

            contexto.Response.Cookies.Append(CookieToken, datos.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure   = contexto.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires  = datos.ExpiraEn
            });

            return Results.Ok(new
            {
                datos.NombreUsuario,
                datos.NombreCompleto,
                datos.Rol,
                datos.DebeCambiarPassword
            });
        });

        grupo.MapPost("/logout", (HttpContext contexto) =>
        {
            contexto.Response.Cookies.Delete(CookieToken);
            return Results.Ok();
        });
    }

    // -------------------------------------------------------------- Kiosco
    // Lo consume el pedestal. Protegido con un token fijo de pantalla en vez
    // de JWT de usuario: en el pedestal no hay nadie que inicie sesion.
    private static void MapearKiosco(WebApplication app)
    {
        var grupo = app.MapGroup("/api/kiosco")
                       .WithTags("Kiosco")
                       .AddEndpointFilter(TokenPantallaFiltro);

        grupo.MapGet("/ruleta/{ruletaId:int}", async (
            int ruletaId, ObtenerRuletaKioscoQueryHandler handler) =>
        {
            var resultado = await handler.EjecutarAsync(new ObtenerRuletaKioscoQuery(ruletaId));
            return resultado.Exito
                ? Results.Ok(resultado.Valor)
                : Results.NotFound(new { mensaje = resultado.Mensaje });
        });

        grupo.MapGet("/pantalla/{codigo}", async (
            string codigo, IPantallaRepositorio pantallas) =>
        {
            var pantalla = await pantallas.ObtenerPorCodigoAsync(codigo);
            if (pantalla is null)
                return Results.NotFound(new { mensaje = "Pantalla no registrada." });

            await pantallas.MarcarConexionAsync(codigo);

            return Results.Ok(new
            {
                pantalla.PantallaId,
                pantalla.Codigo,
                pantalla.Nombre,
                pantalla.RuletaAsignadaId
            });
        });

        // El sorteo pasa por aca: el kiosco recibe el angulo ya calculado y
        // solo anima hasta ahi. No decide nada.
        grupo.MapPost("/girar", async (GirarRequest peticion, GirarCommandHandler handler) =>
        {
            var resultado = await handler.EjecutarAsync(
                new GirarCommand(peticion.RuletaId, peticion.CodigoPantalla));

            return resultado.Exito
                ? Results.Ok(resultado.Valor)
                : Results.BadRequest(new { mensaje = resultado.Mensaje });
        });

        grupo.MapPost("/registrar", async (
            RegistrarClienteRequest peticion, RegistrarClienteCommandHandler handler) =>
        {
            var resultado = await handler.EjecutarAsync(new RegistrarClienteCommand(
                peticion.JugadaId,
                peticion.TipoDocumento,
                peticion.NumeroDocumento,
                peticion.Nombres,
                peticion.ApellidoPaterno,
                peticion.ApellidoMaterno,
                peticion.Correo,
                peticion.Telefono,
                peticion.AceptaWhatsapp,
                peticion.AceptaSms,
                peticion.AceptaLlamada,
                peticion.AceptaEmail));

            return resultado.Exito
                ? Results.Ok(new { mensaje = resultado.Mensaje })
                : Results.BadRequest(new { mensaje = resultado.Mensaje });
        });

        // El kiosco consulta si el documento ya esta registrado para
        // precargar los datos y no hacerle escribir todo de nuevo.
        grupo.MapGet("/cliente/{tipo:int}/{numero}", async (
            int tipo, string numero, IClienteRepositorio clientes) =>
        {
            if (!Enum.IsDefined(typeof(TipoDocumento), (byte)tipo))
                return Results.BadRequest(new { mensaje = "Tipo de documento invalido." });

            var cliente = await clientes.BuscarAsync(
                (TipoDocumento)tipo, numero.Trim().ToUpperInvariant());

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


        // Autocompletado desde el padron externo. Solo para DNI: los otros
        // tipos de documento no estan en ese registro.
        //
        // Nunca devuelve error: si el servicio no responde o esta apagado,
        // contesta "no encontrado" y el cliente escribe a mano. El
        // autocompletado es una comodidad, no un requisito.
        grupo.MapGet("/padron/{numero}", async (
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

        grupo.MapPost("/latido", async (
            LatidoRequest peticion, IPantallaRepositorio pantallas) =>
        {
            await pantallas.MarcarConexionAsync(peticion.CodigoPantalla);
            return Results.Ok();
        });
    }

    // ------------------------------------------------------------- Gestion
    private static void MapearGestion(WebApplication app)
    {
        var grupo = app.MapGroup("/api/gestion")
                       .WithTags("Gestion")
                       .RequireAuthorization();

        grupo.MapPost("/pantalla/asignar", async (
            AsignarRuletaRequest peticion, AsignarRuletaPantallaCommandHandler handler) =>
        {
            var resultado = await handler.EjecutarAsync(
                new AsignarRuletaPantallaCommand(peticion.PantallaId, peticion.RuletaId));

            return resultado.Exito
                ? Results.Ok(new { mensaje = resultado.Mensaje })
                : Results.BadRequest(new { mensaje = resultado.Mensaje });
        });

        grupo.MapGet("/resumen", async (ObtenerResumenQueryHandler handler) =>
            Results.Ok(await handler.EjecutarAsync()));
    }

    /// <summary>
    /// El pedestal se autentica con un token fijo en la cabecera
    /// X-Pantalla-Token. Simple a proposito: es una maquina en la misma PC,
    /// no un usuario.
    /// </summary>
    private static async ValueTask<object?> TokenPantallaFiltro(
        EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        var configuracion = contexto.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>();

        string esperado = configuracion["Kiosco:TokenPantalla"] ?? "";
        string recibido = contexto.HttpContext.Request.Headers["X-Pantalla-Token"].ToString();

        if (string.IsNullOrWhiteSpace(esperado) || recibido != esperado)
            return Results.Unauthorized();

        return await siguiente(contexto);
    }
}
