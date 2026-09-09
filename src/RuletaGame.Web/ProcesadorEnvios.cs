using Microsoft.Extensions.Options;
using RuletaGame.Application.Abstractions;
using RuletaGame.Application.Common;
using RuletaGame.Domain.Entities;
using RuletaGame.Domain.Enums;
using RuletaGame.Infrastructure.Servicios;

namespace RuletaGame.Web;

/// <summary>
/// Procesa la cola de envios al sistema externo.
///
/// Corre aparte del registro a proposito: el cliente esta parado frente al
/// pedestal y no puede esperar a que un servicio de terceros conteste. Se le
/// confirma en cuanto queda guardado localmente, y el envio ocurre despues.
///
/// Los reintentos se espacian: 1 minuto, 5, 15, 30, 60. Reintentar cada pocos
/// segundos contra un servicio caido solo agrega carga sin mejorar nada.
/// </summary>
public sealed class ProcesadorEnvios(
    IServiceProvider servicios,
    IOptions<OpcionesSistemaExterno> opciones,
    ILogger<ProcesadorEnvios> registro) : BackgroundService
{
    /// <summary>Minutos de espera segun el numero de intento fallido.</summary>
    private static readonly int[] EsperaMinutos = [1, 5, 15, 30, 60];

    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        // Margen para que la aplicacion termine de levantar.
        await Task.Delay(TimeSpan.FromSeconds(15), cancelacion);

        while (!cancelacion.IsCancellationRequested)
        {
            try
            {
                await ProcesarTandaAsync(cancelacion);
            }
            catch (Exception ex)
            {
                // Este bucle no puede morir: si muere, los envios se detienen
                // en silencio y nadie se entera hasta que falta medio dia.
                registro.LogError(ex, "Fallo el ciclo de envios");
            }

            await Task.Delay(Intervalo, cancelacion);
        }
    }

    private async Task ProcesarTandaAsync(CancellationToken cancelacion)
    {
        await using var alcance = servicios.CreateAsyncScope();
        var proveedor = alcance.ServiceProvider;

        var configuracion = proveedor.GetRequiredService<IConfiguracionRepositorio>();
        var config = await configuracion.ObtenerDiccionarioAsync();

        if (!config.LeerBool("ENVIAR_CLIENTE_EXTERNO", false)) return;

        var envios   = proveedor.GetRequiredService<IEnvioClienteRepositorio>();
        var clientes = proveedor.GetRequiredService<IClienteRepositorio>();
        var externo  = proveedor.GetRequiredService<ServicioSistemaExterno>();

        var pendientes = await envios.PendientesAsync();
        if (pendientes.Count == 0) return;

        int maxIntentos = config.LeerEntero("ENVIO_MAX_INTENTOS", 5);

        foreach (var envio in pendientes)
        {
            if (cancelacion.IsCancellationRequested) break;

            await ProcesarUnoAsync(envio, envios, clientes, externo, maxIntentos);
        }
    }

    private async Task ProcesarUnoAsync(
        EnvioCliente envio,
        IEnvioClienteRepositorio envios,
        IClienteRepositorio clientes,
        ServicioSistemaExterno externo,
        int maxIntentos)
    {
        var cliente = await clientes.ObtenerAsync(envio.ClienteId);

        if (cliente is null)
        {
            envio.Estado  = EstadoEnvio.Descartado;
            envio.Mensaje = "El cliente ya no existe.";
            envio.ProximoIntento = null;
            await envios.ActualizarAsync(envio);
            return;
        }

        var resultado = await externo.EnviarAsync(
            cliente, opciones.Value, (int)cliente.TipoDocumento);

        envio.Intentos++;
        envio.CodigoHttp    = resultado.CodigoHttp;
        envio.Mensaje       = resultado.Mensaje;
        envio.PeticionJson  = resultado.PeticionJson;
        envio.RespuestaJson = resultado.RespuestaJson;

        if (resultado.Estado is EstadoEnvio.Enviado or EstadoEnvio.Duplicado)
        {
            envio.Estado         = resultado.Estado;
            envio.FechaEnvio     = DateTime.Now;
            envio.ProximoIntento = null;

            registro.LogInformation("Cliente {Doc} -> {Estado}",
                cliente.NumeroDocumento, resultado.Estado);
        }
        else if (envio.Intentos >= maxIntentos)
        {
            // Se deja de reintentar, pero la fila queda con la peticion y la
            // respuesta para que se pueda reenviar a mano desde el gestor.
            envio.Estado         = EstadoEnvio.Descartado;
            envio.ProximoIntento = null;

            registro.LogWarning("Cliente {Doc} descartado tras {Intentos} intentos: {Mensaje}",
                cliente.NumeroDocumento, envio.Intentos, resultado.Mensaje);
        }
        else
        {
            envio.Estado = EstadoEnvio.Fallido;

            int espera = EsperaMinutos[Math.Min(envio.Intentos - 1, EsperaMinutos.Length - 1)];
            envio.ProximoIntento = DateTime.Now.AddMinutes(espera);
        }

        await envios.ActualizarAsync(envio);
    }
}
