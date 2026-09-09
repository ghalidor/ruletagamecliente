using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Domain.Enums;

namespace RuletaGame.Web.Pages.Usuarios;

[Authorize(Roles = $"{Rol.SuperAdmin},{Rol.Admin}")]
public sealed class IndexModel(
    IUsuarioRepositorio usuarios,
    IHasheadorPassword  hasheador) : PageModel
{
    public IReadOnlyList<Usuario> Lista { get; private set; } = [];

    public async Task OnGetAsync() => Lista = await usuarios.ListarAsync();

    public async Task<IActionResult> OnPostGuardarAsync(
        int usuarioId, string nombreUsuario, string nombreCompleto, string rol, string? password)
    {
        if (!Rol.EsValido(rol))
            return new JsonResult(new { exito = false, mensaje = "Rol invalido." });

        if (string.IsNullOrWhiteSpace(nombreCompleto))
            return new JsonResult(new { exito = false, mensaje = "El nombre completo es obligatorio." });

        if (usuarioId == 0)
        {
            if (string.IsNullOrWhiteSpace(nombreUsuario))
                return new JsonResult(new { exito = false, mensaje = "El usuario es obligatorio." });

            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return new JsonResult(new { exito = false, mensaje = "La clave debe tener al menos 8 caracteres." });

            if (await usuarios.ObtenerPorNombreAsync(nombreUsuario.Trim()) is not null)
                return new JsonResult(new { exito = false, mensaje = "Ese usuario ya existe." });

            await usuarios.CrearAsync(new Usuario
            {
                NombreUsuario       = nombreUsuario.Trim(),
                NombreCompleto      = nombreCompleto.Trim(),
                PasswordHash        = hasheador.Hashear(password),
                Rol                 = rol,
                Activo              = true,
                DebeCambiarPassword = true   // que elija su propia clave al entrar
            });

            return new JsonResult(new { exito = true, mensaje = "Usuario creado." });
        }

        var usuario = await usuarios.ObtenerAsync(usuarioId);
        if (usuario is null) return new JsonResult(new { exito = false, mensaje = "No existe." });

        usuario.NombreCompleto = nombreCompleto.Trim();
        usuario.Rol            = rol;
        await usuarios.ActualizarAsync(usuario);

        if (!string.IsNullOrWhiteSpace(password))
        {
            if (password.Length < 8)
                return new JsonResult(new { exito = false, mensaje = "La clave debe tener al menos 8 caracteres." });

            await usuarios.CambiarPasswordAsync(usuarioId, hasheador.Hashear(password));
        }

        return new JsonResult(new { exito = true, mensaje = "Usuario actualizado." });
    }

    public async Task<IActionResult> OnPostToggleAsync(int usuarioId, bool activo)
    {
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int propio);

        // Sin esto es posible dejarse afuera del sistema con un click.
        if (propio == usuarioId && !activo)
            return new JsonResult(new { exito = false, mensaje = "No puedes deshabilitar tu propio usuario." });

        var usuario = await usuarios.ObtenerAsync(usuarioId);
        if (usuario is null) return new JsonResult(new { exito = false, mensaje = "No existe." });

        usuario.Activo = activo;
        await usuarios.ActualizarAsync(usuario);

        return new JsonResult(new { exito = true });
    }
}
