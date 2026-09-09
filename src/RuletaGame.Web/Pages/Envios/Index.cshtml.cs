using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;

namespace RuletaGame.Web.Pages.Envios;

public sealed class IndexModel(IEnvioClienteRepositorio envios) : PageModel
{
    public IReadOnlyList<EnvioCliente> Lista { get; private set; } = [];
    public Dictionary<string, int> Conteo { get; private set; } = [];
    public string? Filtro { get; private set; }

    public async Task OnGetAsync(string? estado)
    {
        Filtro = string.IsNullOrWhiteSpace(estado) ? null : estado;
        Lista  = await envios.ListarAsync(Filtro);
        Conteo = await envios.ContarPorEstadoAsync();
    }

    /// <summary>
    /// Vuelve a poner un envio en cola. Sirve cuando el otro sistema estuvo
    /// caido mucho tiempo y los reintentos automaticos se agotaron.
    /// </summary>
    public async Task<IActionResult> OnPostReintentarAsync(long envioId)
    {
        var envio = await envios.ObtenerAsync(envioId);
        if (envio is null) return new JsonResult(new { exito = false, mensaje = "No existe." });

        if (envio.Estado is EstadoEnvio.Enviado or EstadoEnvio.Duplicado)
            return new JsonResult(new
            {
                exito = false,
                mensaje = "Este envio ya llego al sistema externo."
            });

        envio.Estado         = EstadoEnvio.Pendiente;
        envio.Intentos       = 0;
        envio.ProximoIntento = DateTime.Now;

        await envios.ActualizarAsync(envio);

        return new JsonResult(new { exito = true, mensaje = "Se reintentara en unos segundos." });
    }
}
