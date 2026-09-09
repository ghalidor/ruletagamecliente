namespace RuletaGame.Domain.Enums;

/// <summary>
/// Las dos clases de terminal. Se diferencian en como se autentican y en poco
/// mas: el juego en si es identico.
/// </summary>
public static class TipoTerminal
{
    /// <summary>App Electron en una PC fija. Se autentica con un token fijo.</summary>
    public const string Pedestal = "PEDESTAL";

    /// <summary>Navegador en tablet o celular. Se autentica con una clave.</summary>
    public const string Web = "WEB";

    public static readonly string[] Todos = [Pedestal, Web];

    public static bool EsValido(string? tipo) => tipo is not null && Todos.Contains(tipo);
}
