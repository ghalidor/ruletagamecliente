using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;

namespace RuletaGame.Web.Pages.Ruletas;

public sealed class IndexModel(
    IRuletaRepositorio   ruletas,
    IPantallaRepositorio pantallas) : PageModel
{
    public IReadOnlyList<Ruleta>   Lista     { get; private set; } = [];
    public IReadOnlyList<Pantalla> Pantallas { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Lista     = await ruletas.ListarAsync();
        Pantallas = await pantallas.ListarAsync();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int ruletaId)
    {
        // El historico no se toca: si la ruleta ya tuvo jugadas, se deshabilita
        // en vez de borrarse, para que el reporte siga cuadrando.
        if (await ruletas.TieneJugadasAsync(ruletaId))
            return new JsonResult(new
            {
                exito = false,
                mensaje = "Esta ruleta ya tiene jugadas registradas. Deshabilitala en vez de eliminarla."
            });

        await ruletas.EliminarAsync(ruletaId);
        return new JsonResult(new { exito = true, mensaje = "Ruleta eliminada." });
    }
}
