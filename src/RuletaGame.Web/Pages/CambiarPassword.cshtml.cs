using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using RuletaGame.Application.Abstractions;

namespace RuletaGame.Web.Pages;

public sealed class CambiarPasswordModel(
    IUsuarioRepositorio usuarios,
    IHasheadorPassword  hasheador) : PageModel
{
    [BindProperty] public Entrada Datos { get; set; } = new();

    public sealed class Entrada
    {
        [Required(ErrorMessage = "Escribe tu contrasena actual.")]
        public string Actual { get; set; } = "";

        [Required(ErrorMessage = "Escribe la contrasena nueva.")]
        [MinLength(8, ErrorMessage = "Minimo 8 caracteres.")]
        public string Nueva { get; set; } = "";

        [Required(ErrorMessage = "Repite la contrasena nueva.")]
        [Compare(nameof(Nueva), ErrorMessage = "Las contrasenas no coinciden.")]
        public string Repetir { get; set; } = "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int usuarioId))
            return RedirectToPage("Login");

        var usuario = await usuarios.ObtenerAsync(usuarioId);
        if (usuario is null) return RedirectToPage("Login");

        if (!hasheador.Verificar(Datos.Actual, usuario.PasswordHash))
        {
            ModelState.AddModelError("Datos.Actual", "La contrasena actual no es correcta.");
            return Page();
        }

        if (Datos.Actual == Datos.Nueva)
        {
            ModelState.AddModelError("Datos.Nueva", "La nueva contrasena debe ser distinta de la actual.");
            return Page();
        }

        await usuarios.CambiarPasswordAsync(usuarioId, hasheador.Hashear(Datos.Nueva));

        // El token viejo todavia dice debe_cambiar = 1, asi que hay que
        // reingresar para que se emita uno nuevo.
        Response.Cookies.Delete(Api.EndpointsApi.CookieToken);

        TempData["Mensaje"] = "Contrasena actualizada. Ingresa de nuevo.";
        return RedirectToPage("Login");
    }
}
