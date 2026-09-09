using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;
using System.Security.Claims;
using RuletaGame.Application.Abstractions;
using RuletaGame.Application.Servicios;
using RuletaGame.Domain.Entities;

namespace RuletaGame.Web.Pages.Configuracion;

public sealed class IndexModel(
    IConfiguracionRepositorio configuracion,
    ITopeDiarioRepositorio    topes,
    TopeService               topeServicio) : PageModel
{
    public IReadOnlyList<Domain.Entities.Configuracion> Lista { get; private set; } = [];
    public IReadOnlyList<TopeDiario> Excepciones { get; private set; } = [];
    public EstadoTope EstadoHoy { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Lista       = await configuracion.ListarAsync();
        Excepciones = await topes.ListarDesdeAsync(DateTime.Today.AddDays(-7));
        EstadoHoy   = await topeServicio.ObtenerHoyAsync();
    }

    public async Task<IActionResult> OnPostGuardarAsync(string clave, string valor)
    {
        valor = (valor ?? "").Trim();

        if (string.IsNullOrWhiteSpace(valor))
            return new JsonResult(new { exito = false, mensaje = "El valor no puede quedar vacio." });

        var item = (await configuracion.ListarAsync()).FirstOrDefault(c => c.Clave == clave);
        if (item is null)
            return new JsonResult(new { exito = false, mensaje = "Clave desconocida." });

        // Validacion por tipo: sin esto un valor con letras tumbaba la pantalla
        // del kiosco, que es lo que pasaba en el sistema legacy.
        string? error = item.TipoDato switch
        {
            "ENTERO"  when !int.TryParse(valor, out _) => "Debe ser un numero entero.",
            "DECIMAL" when !decimal.TryParse(valor, NumberStyles.Any,
                              CultureInfo.InvariantCulture, out _) => "Debe ser un numero.",
            "BOOL"    when valor is not ("0" or "1") => "Debe ser 0 o 1.",
            _ => null
        };

        if (error is not null)
            return new JsonResult(new { exito = false, mensaje = error });

        await configuracion.ActualizarAsync(clave, valor);
        return new JsonResult(new { exito = true, mensaje = "Guardado." });
    }

    public async Task<IActionResult> OnPostTopeFechaAsync(string fecha, decimal monto, string? nota)
    {
        if (!TryFecha(fecha, out var dia))
            return new JsonResult(new { exito = false, mensaje = "Fecha invalida." });

        if (monto < 0)
            return new JsonResult(new { exito = false, mensaje = "El monto no puede ser negativo." });

        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int usuarioId);

        await topes.GuardarAsync(new TopeDiario
        {
            Fecha     = dia,
            Monto     = monto,
            Nota      = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim(),
            UsuarioId = usuarioId > 0 ? usuarioId : null
        });

        return new JsonResult(new { exito = true, mensaje = $"Tope de {dia:dd/MM/yyyy} guardado." });
    }

    public async Task<IActionResult> OnPostQuitarTopeAsync(string fecha)
    {
        if (!TryFecha(fecha, out var dia))
            return new JsonResult(new { exito = false, mensaje = "Fecha invalida." });

        await topes.EliminarAsync(dia);
        return new JsonResult(new { exito = true, mensaje = "Excepcion eliminada." });
    }

    private static bool TryFecha(string? texto, out DateTime fecha) =>
        DateTime.TryParseExact(texto ?? "", "d/M/yyyy",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);
}
