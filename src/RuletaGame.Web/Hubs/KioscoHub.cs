using Microsoft.AspNetCore.SignalR;
using RuletaGame.Application.Abstractions;

namespace RuletaGame.Web.Hubs;

/// <summary>
/// Canal en tiempo real hacia los pedestales. Cada kiosco entra a un grupo con
/// su codigo, asi el gestor avisa solo a la pantalla que corresponde.
/// </summary>
public sealed class KioscoHub(IPantallaRepositorio pantallas) : Hub
{
    /// <summary>El kiosco se identifica al conectarse.</summary>
    public async Task Registrar(string codigoPantalla)
    {
        if (string.IsNullOrWhiteSpace(codigoPantalla)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, codigoPantalla);
        await pantallas.MarcarConexionAsync(codigoPantalla);

        // Le devolvemos la ruleta asignada: al reconectar puede haber cambiado
        // mientras estuvo caido, y no queremos que se quede en blanco.
        var pantalla = await pantallas.ObtenerPorCodigoAsync(codigoPantalla);
        await Clients.Caller.SendAsync("RuletaAsignada", pantalla?.RuletaAsignadaId);
    }

    /// <summary>Latido periodico para el indicador "en linea" del gestor.</summary>
    public Task Latido(string codigoPantalla) => pantallas.MarcarConexionAsync(codigoPantalla);
}

/// <summary>Implementacion de la interfaz que usa la capa Application.</summary>
public sealed class NotificadorKiosco(IHubContext<KioscoHub> hub) : INotificadorKiosco
{
    public Task CambiarRuletaAsync(string codigoPantalla, int? ruletaId) =>
        hub.Clients.Group(codigoPantalla).SendAsync("RuletaAsignada", ruletaId);

    public Task RecargarTajadasAsync(int ruletaId) =>
        hub.Clients.All.SendAsync("TajadasActualizadas", ruletaId);
}
