using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using RuletaGame.Application.Abstractions;

namespace RuletaGame.Web.Pages.Ruletas;

public sealed class EditarModel(IRuletaRepositorio ruletas) : PageModel
{
    [BindProperty] public Entrada Datos { get; set; } = new();

    public bool EsNueva => Datos.RuletaId == 0;

    public sealed class Entrada
    {
        public int RuletaId { get; set; }

        [Required(ErrorMessage = "Escribe el nombre de la ruleta.")]
        [StringLength(150, ErrorMessage = "Maximo 150 caracteres.")]
        public string Nombre { get; set; } = "";

        [StringLength(500, ErrorMessage = "Maximo 500 caracteres.")]
        public string? Descripcion { get; set; }

        public bool Activo { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(int? ruletaId)
    {
        if (ruletaId is null or 0) return Page();

        var ruleta = await ruletas.ObtenerAsync(ruletaId.Value);
        if (ruleta is null) return RedirectToPage("Index");

        Datos = new Entrada
        {
            RuletaId    = ruleta.RuletaId,
            Nombre      = ruleta.Nombre,
            Descripcion = ruleta.Descripcion,
            Activo      = ruleta.Activo
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (Datos.RuletaId == 0)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int usuarioId);

            int nuevoId = await ruletas.CrearAsync(new Domain.Entities.Ruleta
            {
                Nombre            = Datos.Nombre.Trim(),
                Descripcion       = Datos.Descripcion?.Trim(),
                Activo            = Datos.Activo,
                UsuarioCreacionId = usuarioId > 0 ? usuarioId : null
            });

            // Recien creada no tiene premios: mandamos directo a cargarlos.
            return RedirectToPage("/Tajadas/Index", new { ruletaId = nuevoId, creada = true });
        }

        var ruleta = await ruletas.ObtenerAsync(Datos.RuletaId);
        if (ruleta is null) return RedirectToPage("Index");

        ruleta.Nombre      = Datos.Nombre.Trim();
        ruleta.Descripcion = Datos.Descripcion?.Trim();
        ruleta.Activo      = Datos.Activo;

        await ruletas.ActualizarAsync(ruleta);

        TempData["Mensaje"] = "Ruleta actualizada.";
        return RedirectToPage("Index");
    }
}
