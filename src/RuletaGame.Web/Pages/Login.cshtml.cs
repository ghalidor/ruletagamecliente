using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RuletaGame.Web.Pages;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    // La pagina solo pinta el formulario; el login real pasa por
    // /api/auth/login, que emite el JWT y lo deja en la cookie HttpOnly.
}
