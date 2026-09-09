using RuletaGame.Application.Abstractions;
using Serilog;

namespace RuletaGame.Web;

/// <summary>
/// El script SQL crea el usuario admin con el texto SEMBRAR_EN_ARRANQUE en
/// lugar del hash, porque el hash depende del algoritmo de la aplicacion y no
/// tiene sentido escribirlo a mano en un .sql. Aca se reemplaza por el hash
/// real la primera vez que el servicio levanta.
/// </summary>
public static class SembradorInicial
{
    private const string Marcador        = "SEMBRAR_EN_ARRANQUE";
    private const string PasswordInicial = "Admin.2026";

    public static async Task EjecutarAsync(IServiceProvider servicios)
    {
       // using var alcance = servicios.CreateScope();
        await using var alcance = servicios.CreateAsyncScope();
        var usuarios  = alcance.ServiceProvider.GetRequiredService<IUsuarioRepositorio>();
        var hasheador = alcance.ServiceProvider.GetRequiredService<IHasheadorPassword>();

        try
        {
            var admin = await usuarios.ObtenerPorNombreAsync("admin");

            if (admin is null)
            {
                Log.Warning("No existe el usuario admin. Corre database/02_datos_iniciales.sql.");
                return;
            }

            if (admin.PasswordHash != Marcador) return;   // ya fue sembrado

            await usuarios.CambiarPasswordAsync(admin.UsuarioId, hasheador.Hashear(PasswordInicial));

            Log.Warning(
                "Usuario admin sembrado con la clave inicial {Clave}. " +
                "El sistema pedira cambiarla en el primer ingreso.", PasswordInicial);
        }
        catch (Exception ex)
        {
            // Si la base todavia no existe, el servicio debe arrancar igual
            // para que /salud responda y se pueda diagnosticar.
            Log.Error(ex, "No se pudo sembrar el usuario inicial");
        }
    }
}
