using RuletaGame.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace RuletaGame.Infrastructure.Servicios;

/// <summary>
/// Reemplaza System.Drawing del sistema legacy, que no funciona en .NET
/// moderno fuera de Windows. Ademas guarda en WebP: el original redimensionaba
/// a 882x386 y guardaba como JPEG aunque el archivo mantuviera extension .png.
/// </summary>
public sealed class ServicioImagenes(string carpetaRaiz) : IServicioImagenes
{
    private const int AnchoMaximo = 900;
    private const int AltoMaximo = 400;

    private static readonly string[] ExtensionesValidas =
        [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"];

    public async Task<string> GuardarAsync(Stream archivo, string nombreOriginal, int ruletaId)
    {
        string extension = Path.GetExtension(nombreOriginal).ToLowerInvariant();
        if(!ExtensionesValidas.Contains(extension))
            throw new InvalidOperationException(
                $"Formato no permitido. Usa: {string.Join(", ", ExtensionesValidas)}");

        // Sin esta guarda, una raiz vacia revienta con un ArgumentNullException
        // opaco dentro de Path.Combine, que no dice nada util.
        if(string.IsNullOrWhiteSpace(carpetaRaiz))
            throw new InvalidOperationException(
                "No se pudo determinar la carpeta wwwroot para guardar la imagen.");

        string carpeta = Path.Combine(carpetaRaiz, "premios", ruletaId.ToString());
        Directory.CreateDirectory(carpeta);

        string nombre = $"{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..28] + ".webp";
        string destino = Path.Combine(carpeta, nombre);

        using var imagen = await Image.LoadAsync(archivo);

        // Modo Max: no agranda las chicas ni deforma las grandes.
        imagen.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(AnchoMaximo, AltoMaximo),
            Mode = ResizeMode.Max
        }));

        await imagen.SaveAsync(destino, new WebpEncoder { Quality = 82 });

        return $"/premios/{ruletaId}/{nombre}";
    }

    public void Eliminar(string? rutaRelativa)
    {
        if(string.IsNullOrWhiteSpace(rutaRelativa)) return;

        try
        {
            string ruta = Path.Combine(carpetaRaiz, rutaRelativa.TrimStart('/', '\\'));

            // No dejamos que una ruta manipulada salga de la carpeta de assets.
            string completa = Path.GetFullPath(ruta);
            if(!completa.StartsWith(Path.GetFullPath(carpetaRaiz), StringComparison.OrdinalIgnoreCase))
                return;

            if(File.Exists(completa)) File.Delete(completa);
        }
        catch(IOException)
        {
            // Si el archivo esta en uso, no vale la pena tumbar la operacion.
        }
    }
}