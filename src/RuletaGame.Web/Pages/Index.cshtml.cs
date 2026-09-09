using Microsoft.AspNetCore.Mvc.RazorPages;
using RuletaGame.Application.Dtos;
using RuletaGame.Application.Queries;

namespace RuletaGame.Web.Pages;

public sealed class IndexModel(ObtenerResumenQueryHandler resumen) : PageModel
{
    public ResumenDto Resumen { get; private set; } = new();

    public async Task OnGetAsync() => Resumen = await resumen.EjecutarAsync();
}
