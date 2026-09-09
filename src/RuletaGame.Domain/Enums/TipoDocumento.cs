namespace RuletaGame.Domain.Enums;

public enum TipoDocumento : byte
{
    Dni = 1,
    CarnetExtranjeria = 2,
    Otros = 3
}

/// <summary>
/// Reglas de cada tipo de documento, en un solo lugar.
///
/// Estan en Domain a proposito: el kiosco valida para dar respuesta inmediata,
/// pero la validacion que manda es esta. Si solo viviera en el JavaScript,
/// bastaria abrir la consola para saltarsela.
/// </summary>
public static class ReglasDocumento
{
    public static string Nombre(TipoDocumento tipo) => tipo switch
    {
        TipoDocumento.Dni => "DNI",
        TipoDocumento.CarnetExtranjeria => "Carnet de extranjeria",
        TipoDocumento.Otros => "Otro documento",
        _ => "Documento"
    };

    /// <summary>
    /// Version corta para los botones del pedestal. El nombre completo no cabe
    /// y terminaba recortado con puntos suspensivos, que se lee peor que una
    /// abreviatura pensada.
    /// </summary>
    public static string NombreCorto(TipoDocumento tipo) => tipo switch
    {
        TipoDocumento.Dni => "DNI",
        TipoDocumento.CarnetExtranjeria => "Carnet ext.",
        TipoDocumento.Otros => "Otro",
        _ => "Documento"
    };

    /// <summary>Solo el DNI es estrictamente numerico.</summary>
    public static bool SoloNumeros(TipoDocumento tipo) => tipo == TipoDocumento.Dni;

    public static (int Minimo, int Maximo) Longitud(TipoDocumento tipo) => tipo switch
    {
        TipoDocumento.Dni => (8, 8),
        TipoDocumento.CarnetExtranjeria => (9, 12),
        // Tope de 15: con mas casillas quedan tan angostas en el pedestal que
        // no se distingue lo que uno escribio.
        _ => (5, 15)
    };

    /// <summary>Devuelve null si es valido, o el motivo del rechazo.</summary>
    public static string? Validar(TipoDocumento tipo, string? numero)
    {
        numero = (numero ?? "").Trim();

        if(numero.Length == 0)
            return "Escribe el numero de documento.";

        var (minimo, maximo) = Longitud(tipo);

        if(numero.Length < minimo || numero.Length > maximo)
            return minimo == maximo
                ? $"El {Nombre(tipo)} debe tener {minimo} caracteres."
                : $"El {Nombre(tipo)} debe tener entre {minimo} y {maximo} caracteres.";

        if(SoloNumeros(tipo) && !numero.All(char.IsDigit))
            return $"El {Nombre(tipo)} solo admite numeros.";

        if(!numero.All(c => char.IsLetterOrDigit(c) || c == '-'))
            return "El documento solo admite letras, numeros y guiones.";

        return null;
    }
}