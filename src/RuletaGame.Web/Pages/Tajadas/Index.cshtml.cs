using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Domain.Enums;

namespace RuletaGame.Web.Pages.Tajadas;

public sealed class IndexModel(
    IRuletaRepositorio ruletas,
    ITajadaRepositorio tajadas,
    IConfiguracionRepositorio configuracion,
    IServicioImagenes imagenes,
    INotificadorKiosco notificador) : PageModel
{
    public Ruleta? Ruleta { get; private set; }
    public IReadOnlyList<Tajada> Lista { get; private set; } = [];
    public string Moneda { get; private set; } = "S/.";
    public decimal SumaPorcentaje { get; private set; }

    public async Task<IActionResult> OnGetAsync(int ruletaId)
    {
        Ruleta = await ruletas.ObtenerAsync(ruletaId);
        if(Ruleta is null) return RedirectToPage("/Ruletas/Index");

        Lista = await tajadas.ListarPorRuletaAsync(ruletaId);

        var config = await configuracion.ObtenerDiccionarioAsync();
        Moneda = config.GetValueOrDefault(ClaveConfiguracion.MonedaSimbolo, "S/.");

        SumaPorcentaje = Lista.Where(t => t.Activo).Sum(t => t.Probabilidad ?? 0m);

        return Page();
    }

    public async Task<IActionResult> OnPostToggleAsync(int tajadaId, bool activo)
    {
        await tajadas.CambiarActivoAsync(tajadaId, activo);
        await AvisarAlPedestalAsync(tajadaId);

        return new JsonResult(new { exito = true });
    }

    /// <summary>
    /// Avisa al pedestal que la rueda cambio.
    ///
    /// Sin esto el kiosco sigue dibujando las tajadas que cargo al inicio,
    /// mientras el servidor ya sortea con las nuevas. Y como el angulo se
    /// calcula sobre la cantidad real de tajadas, la rueda frenaria en el
    /// sector equivocado: el puntero caeria en un premio y el cartel diria otro.
    /// </summary>
    private async Task AvisarAlPedestalAsync(int tajadaId)
    {
        var tajada = await tajadas.ObtenerAsync(tajadaId);
        if(tajada is not null)
            await notificador.RecargarTajadasAsync(tajada.RuletaId);
    }

    public async Task<IActionResult> OnPostToggleTopeAsync(int tajadaId, bool afectaTope)
    {
        var tajada = await tajadas.ObtenerAsync(tajadaId);
        if(tajada is null) return new JsonResult(new { exito = false, mensaje = "No existe." });

        tajada.AfectaTope = afectaTope;
        await tajadas.ActualizarAsync(tajada);

        return new JsonResult(new { exito = true });
    }

    /// <summary>
    /// La suma se valida AQUI, en el servidor. En el sistema legacy el control
    /// de "no pasar de 100%" corria solo en el navegador y unicamente al
    /// presionar Enter, asi que era trivial saltarselo.
    /// </summary>
    public async Task<IActionResult> OnPostProbabilidadAsync(int tajadaId, decimal? porcentaje)
    {
        if(porcentaje is < 0 or > 100)
            return new JsonResult(new { exito = false, mensaje = "El porcentaje va de 0 a 100." });

        var tajada = await tajadas.ObtenerAsync(tajadaId);
        if(tajada is null) return new JsonResult(new { exito = false, mensaje = "No existe." });

        decimal fraccion = (porcentaje ?? 0m) / 100m;
        decimal resto = await tajadas.SumaProbabilidadesAsync(tajada.RuletaId, tajadaId);

        if(resto + fraccion > 1.0001m)   // margen por redondeo decimal
            return new JsonResult(new
            {
                exito = false,
                mensaje = $"La suma pasaria de 100%. Disponible: {(1m - resto) * 100m:0.##}%"
            });

        await tajadas.ActualizarProbabilidadAsync(tajadaId, fraccion > 0 ? fraccion : null);

        // El sorteo ya toma el valor nuevo (lo lee de la base en cada giro),
        // pero se avisa igual para mantener al pedestal sincronizado.
        await notificador.RecargarTajadasAsync(tajada.RuletaId);

        return new JsonResult(new { exito = true, suma = Math.Round((resto + fraccion) * 100m, 2) });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int tajadaId)
    {
        var tajada = await tajadas.ObtenerAsync(tajadaId);
        if(tajada is null) return new JsonResult(new { exito = false, mensaje = "No existe." });

        int ruletaId = tajada.RuletaId;

        imagenes.Eliminar(tajada.Imagen);
        await tajadas.EliminarAsync(tajadaId);

        // Se avisa despues de borrar, con el id guardado: la tajada ya no existe.
        await notificador.RecargarTajadasAsync(ruletaId);

        return new JsonResult(new { exito = true, mensaje = "Premio eliminado." });
    }
}