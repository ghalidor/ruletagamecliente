using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Domain.Enums;

namespace RuletaGame.Web.Pages.Tajadas;

public sealed class EditarModel(
    IRuletaRepositorio ruletas,
    ITajadaRepositorio tajadas,
    IConfiguracionRepositorio configuracion,
    IServicioImagenes imagenes,
    INotificadorKiosco notificador) : PageModel
{
    [BindProperty] public Entrada Datos { get; set; } = new();
    [BindProperty] public IFormFile? Archivo { get; set; }

    public Ruleta? Ruleta { get; private set; }
    public string Moneda { get; private set; } = "S/.";
    public string? ImagenActual { get; private set; }

    public bool EsNuevo => Datos.TajadaId == 0;

    public sealed class Entrada
    {
        public int TajadaId { get; set; }
        public int RuletaId { get; set; }

        [Required(ErrorMessage = "Escribe una descripcion.")]
        [StringLength(200, ErrorMessage = "Maximo 200 caracteres.")]
        public string Descripcion { get; set; } = "";

        public TipoTajada Tipo { get; set; } = TipoTajada.Monto;

        [Range(0, 999999, ErrorMessage = "El monto no puede ser negativo.")]
        public decimal Monto { get; set; }

        public bool Activo { get; set; } = true;
        public bool AfectaTope { get; set; } = true;
    }

    private async Task CargarContextoAsync(int ruletaId)
    {
        Ruleta = await ruletas.ObtenerAsync(ruletaId);

        var config = await configuracion.ObtenerDiccionarioAsync();
        Moneda = config.GetValueOrDefault(ClaveConfiguracion.MonedaSimbolo, "S/.");
    }

    public async Task<IActionResult> OnGetAsync(int ruletaId, int? tajadaId)
    {
        await CargarContextoAsync(ruletaId);
        if(Ruleta is null) return RedirectToPage("/Ruletas/Index");

        Datos.RuletaId = ruletaId;

        if(tajadaId is null or 0) return Page();

        var tajada = await tajadas.ObtenerAsync(tajadaId.Value);
        if(tajada is null) return RedirectToPage("Index", new { ruletaId });

        Datos = new Entrada
        {
            TajadaId = tajada.TajadaId,
            RuletaId = tajada.RuletaId,
            Descripcion = tajada.Descripcion,
            Tipo = tajada.Tipo,
            Monto = tajada.Monto,
            Activo = tajada.Activo,
            AfectaTope = tajada.AfectaTope
        };

        ImagenActual = tajada.Imagen;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await CargarContextoAsync(Datos.RuletaId);
        if(Ruleta is null) return RedirectToPage("/Ruletas/Index");

        var existente = Datos.TajadaId > 0 ? await tajadas.ObtenerAsync(Datos.TajadaId) : null;
        ImagenActual = existente?.Imagen;

        // Un producto sin imagen se dibuja vacio en la rueda.
        if(Datos.Tipo == TipoTajada.Producto && Archivo is null
            && string.IsNullOrWhiteSpace(ImagenActual))
            ModelState.AddModelError("Archivo", "Los premios de tipo Producto necesitan una imagen.");

        if(!ModelState.IsValid) return Page();

        string? rutaImagen = ImagenActual;

        if(Archivo is { Length: > 0 })
        {
            try
            {
                using var flujo = Archivo.OpenReadStream();
                rutaImagen = await imagenes.GuardarAsync(flujo, Archivo.FileName, Datos.RuletaId);

                if(!string.IsNullOrWhiteSpace(ImagenActual)) imagenes.Eliminar(ImagenActual);
            }
            catch(InvalidOperationException ex)
            {
                ModelState.AddModelError("Archivo", ex.Message);
                return Page();
            }
        }

        // Si paso a Monto, la imagen ya no se usa: la borramos del disco.
        if(Datos.Tipo == TipoTajada.Monto && !string.IsNullOrWhiteSpace(rutaImagen))
        {
            imagenes.Eliminar(rutaImagen);
            rutaImagen = null;
        }

        if(existente is null)
        {
            await tajadas.CrearAsync(new Tajada
            {
                RuletaId = Datos.RuletaId,
                Descripcion = Datos.Descripcion.Trim(),
                Tipo = Datos.Tipo,
                Monto = Datos.Monto,
                Imagen = rutaImagen,
                Orden = await tajadas.SiguienteOrdenAsync(Datos.RuletaId),
                Activo = Datos.Activo,
                AfectaTope = Datos.AfectaTope,
                Probabilidad = null   // se asigna desde el listado
            });
        }
        else
        {
            existente.Descripcion = Datos.Descripcion.Trim();
            existente.Tipo = Datos.Tipo;
            existente.Monto = Datos.Monto;
            existente.Imagen = rutaImagen;
            existente.Activo = Datos.Activo;
            existente.AfectaTope = Datos.AfectaTope;

            await tajadas.ActualizarAsync(existente);
        }

        // El pedestal redibuja la rueda con el premio nuevo o editado.
        await notificador.RecargarTajadasAsync(Datos.RuletaId);

        TempData["Mensaje"] = "Premio guardado.";
        return RedirectToPage("Index", new { ruletaId = Datos.RuletaId });
    }
}