using System.Globalization;

namespace RuletaGame.Application.Common;

/// <summary>
/// Lectura tolerante del diccionario de configuracion. Si una clave falta o
/// trae basura, devuelve el valor por defecto en vez de reventar: el sistema
/// legacy hacia config[0].CO_valor sin validar y tumbaba la pantalla del kiosco.
/// </summary>
public static class ConfiguracionExtensiones
{
    public static string LeerTexto(this Dictionary<string, string> config, string clave, string porDefecto)
        => config.TryGetValue(clave, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : porDefecto;

    public static int LeerEntero(this Dictionary<string, string> config, string clave, int porDefecto)
        => config.TryGetValue(clave, out var v) && int.TryParse(v, out var n) ? n : porDefecto;

    public static decimal LeerDecimal(this Dictionary<string, string> config, string clave, decimal porDefecto)
        => config.TryGetValue(clave, out var v)
           && decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)
            ? n : porDefecto;

    public static bool LeerBool(this Dictionary<string, string> config, string clave, bool porDefecto)
    {
        if (!config.TryGetValue(clave, out var v)) return porDefecto;
        return v.Trim() is "1" or "true" or "True" or "SI" or "si";
    }
}
