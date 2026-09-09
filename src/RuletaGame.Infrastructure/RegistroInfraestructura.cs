using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RuletaGame.Application.Abstractions;
using RuletaGame.Application.Commands;
using RuletaGame.Application.Queries;
using RuletaGame.Application.Servicios;
using RuletaGame.Infrastructure.Persistencia;
using RuletaGame.Infrastructure.Repositorios;
using RuletaGame.Infrastructure.Seguridad;
using RuletaGame.Infrastructure.Servicios;

namespace RuletaGame.Infrastructure;

public static class RegistroInfraestructura
{
    public static IServiceCollection AgregarInfraestructura(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        string cadena = configuracion.GetConnectionString("Kiosco")
            ?? throw new InvalidOperationException(
                "Falta ConnectionStrings:Kiosco en appsettings.json.");

        // Una sesion (conexion) por request, compartida por los repositorios.
        servicios.AddScoped<IDbSesion>(_ => new DbSesion(cadena));

        servicios.AddScoped<IRuletaRepositorio,        RuletaRepositorio>();
        servicios.AddScoped<ITajadaRepositorio,        TajadaRepositorio>();
        servicios.AddScoped<IJugadaRepositorio,        JugadaRepositorio>();
        servicios.AddScoped<ICicloRepositorio,         CicloRepositorio>();
        servicios.AddScoped<IPantallaRepositorio,      PantallaRepositorio>();
        servicios.AddScoped<IConfiguracionRepositorio, ConfiguracionRepositorio>();
        servicios.AddScoped<ITopeDiarioRepositorio,    TopeDiarioRepositorio>();
        servicios.AddScoped<ITopePantallaRepositorio,  TopePantallaRepositorio>();
        servicios.AddScoped<IUsuarioRepositorio,       UsuarioRepositorio>();
        servicios.AddScoped<IClienteRepositorio,       ClienteRepositorio>();
        servicios.AddScoped<IEnvioClienteRepositorio,  EnvioClienteRepositorio>();

        servicios.AddSingleton<IHasheadorPassword, HasheadorPassword>();
        servicios.AddScoped<IGeneradorToken,       GeneradorToken>();

        servicios.AddSingleton<SorteoService>();
        servicios.AddScoped<TopeService>();
        servicios.AddScoped<TerminalService>();

        servicios.Configure<OpcionesJwt>(configuracion.GetSection(OpcionesJwt.Seccion));
        servicios.Configure<OpcionesPadron>(configuracion.GetSection(OpcionesPadron.Seccion));

        // HttpClient propio para el padron: aislado del resto para que un
        // servicio externo lento no consuma las conexiones de la aplicacion.
        servicios.AddHttpClient<ServicioPadron>(cliente =>
            cliente.Timeout = TimeSpan.FromSeconds(10));

        servicios.Configure<OpcionesSistemaExterno>(
            configuracion.GetSection(OpcionesSistemaExterno.Seccion));

        servicios.AddHttpClient<ServicioSistemaExterno>(cliente =>
            cliente.Timeout = TimeSpan.FromSeconds(20));
        servicios.Configure<OpcionesSuperUsuario>(configuracion.GetSection(OpcionesSuperUsuario.Seccion));

        return servicios;
    }

    /// <summary>
    /// Casos de uso. Sin bus de mensajes: cada handler es una clase con un
    /// metodo EjecutarAsync y se inyecta directo. Es CQRS (comandos separados
    /// de consultas) sin la ceremonia de un mediador, que en un proyecto de
    /// este tamano solo agrega indireccion y una dependencia mas.
    /// </summary>
    public static IServiceCollection AgregarCasosDeUso(this IServiceCollection servicios)
    {
        servicios.AddScoped<GirarCommandHandler>();
        servicios.AddScoped<RegistrarClienteCommandHandler>();
        servicios.AddScoped<LoginCommandHandler>();
        servicios.AddScoped<AsignarRuletaPantallaCommandHandler>();

        servicios.AddScoped<ObtenerRuletaKioscoQueryHandler>();
        servicios.AddScoped<ObtenerResumenQueryHandler>();

        return servicios;
    }
}
