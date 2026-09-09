using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using RuletaGame.Application.Abstractions;
using RuletaGame.Application.Commands;
using RuletaGame.Application.Servicios;
using RuletaGame.Domain.Entities;
using RuletaGame.Domain.Enums;

namespace RuletaGame.Web.Pages.Pantallas;

public sealed class IndexModel(
    IPantallaRepositorio pantallas,
    IRuletaRepositorio ruletas,
    ITopePantallaRepositorio topesPantalla,
    TerminalService terminales,
    AsignarRuletaPantallaCommandHandler asignar) : PageModel
{
    public IReadOnlyList<Pantalla> Lista { get; private set; } = [];
    public IReadOnlyList<Ruleta> Ruletas { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Lista = await pantallas.ListarAsync();
        Ruletas = await ruletas.ListarAsync(soloActivas: true);
    }

    /// <summary>La tablet decide que ruleta queda cargada en el pedestal.</summary>
    public async Task<IActionResult> OnPostAsignarAsync(int pantallaId, int? ruletaId)
    {
        var resultado = await asignar.EjecutarAsync(
            new AsignarRuletaPantallaCommand(pantallaId, ruletaId == 0 ? null : ruletaId));

        return new JsonResult(new { exito = resultado.Exito, mensaje = resultado.Mensaje });
    }

    public async Task<IActionResult> OnPostGuardarAsync(
        int pantallaId, string codigo, string nombre, string tipo,
        decimal? topeMonto, string? clave)
    {
        if(string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(nombre))
            return new JsonResult(new { exito = false, mensaje = "Codigo y nombre son obligatorios." });

        if(!TipoTerminal.EsValido(tipo))
            return new JsonResult(new { exito = false, mensaje = "Tipo de terminal invalido." });

        if(topeMonto is < 0)
            return new JsonResult(new { exito = false, mensaje = "El tope no puede ser negativo." });

        bool esWeb = tipo == TipoTerminal.Web;

        if(pantallaId == 0)
        {
            if(await pantallas.ObtenerPorCodigoAsync(codigo.Trim()) is not null)
                return new JsonResult(new { exito = false, mensaje = "Ya existe una terminal con ese codigo." });

            // Una terminal web sin clave no se puede abrir: es obligatoria.
            if(esWeb && (string.IsNullOrWhiteSpace(clave) || clave.Trim().Length < 4))
                return new JsonResult(new
                {
                    exito = false,
                    mensaje = "La terminal web necesita una clave de al menos 4 caracteres."
                });

            await pantallas.CrearAsync(new Pantalla
            {
                Codigo = codigo.Trim(),
                Nombre = nombre.Trim(),
                Tipo = tipo,
                TopeMonto = topeMonto,
                ClaveHash = esWeb ? terminales.HashearClave(clave!) : null,
                Activo = true
            });

            return new JsonResult(new { exito = true, mensaje = "Terminal registrada." });
        }

        var pantalla = await pantallas.ObtenerAsync(pantallaId);
        if(pantalla is null) return new JsonResult(new { exito = false, mensaje = "No existe." });

        pantalla.Codigo = codigo.Trim();
        pantalla.Nombre = nombre.Trim();
        pantalla.Tipo = tipo;
        pantalla.TopeMonto = topeMonto;

        await pantallas.ActualizarAsync(pantalla);

        // La clave solo se cambia si escribieron una nueva: dejarla vacia
        // significa "no la toques", no "borrala".
        if(esWeb && !string.IsNullOrWhiteSpace(clave))
        {
            if(clave.Trim().Length < 4)
                return new JsonResult(new { exito = false, mensaje = "La clave debe tener al menos 4 caracteres." });

            await pantallas.CambiarClaveAsync(pantallaId, terminales.HashearClave(clave));
        }

        return new JsonResult(new { exito = true, mensaje = "Terminal actualizada." });
    }

    /// <summary>Excepcion de tope de una terminal para una fecha puntual.</summary>
    public async Task<IActionResult> OnPostTopeFechaAsync(
        int pantallaId, string fecha, decimal monto, string? nota)
    {
        if(!DateTime.TryParseExact(fecha, "d/M/yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dia))
            return new JsonResult(new { exito = false, mensaje = "Fecha invalida." });

        if(monto < 0)
            return new JsonResult(new { exito = false, mensaje = "El monto no puede ser negativo." });

        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int usuarioId);

        await topesPantalla.GuardarAsync(new TopePantalla
        {
            PantallaId = pantallaId,
            Fecha = dia,
            Monto = monto,
            Nota = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim(),
            UsuarioId = usuarioId > 0 ? usuarioId : null
        });

        return new JsonResult(new { exito = true, mensaje = $"Tope de {dia:dd/MM/yyyy} guardado." });
    }
}
