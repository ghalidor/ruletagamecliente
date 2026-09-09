namespace RuletaGame.Application.Common;

/// <summary>
/// Resultado explicito de un caso de uso. Evita usar excepciones para reglas
/// de negocio, que es caro y esconde el flujo real.
/// </summary>
public sealed class Resultado<T>
{
    public bool    Exito   { get; private init; }
    public T?      Valor   { get; private init; }
    public string? Mensaje { get; private init; }

    public static Resultado<T> Ok(T valor, string? mensaje = null)
        => new() { Exito = true, Valor = valor, Mensaje = mensaje };

    public static Resultado<T> Fallo(string mensaje)
        => new() { Exito = false, Mensaje = mensaje };
}

public sealed class Resultado
{
    public bool    Exito   { get; private init; }
    public string? Mensaje { get; private init; }

    public static Resultado Ok(string? mensaje = null) => new() { Exito = true,  Mensaje = mensaje };
    public static Resultado Fallo(string mensaje)      => new() { Exito = false, Mensaje = mensaje };
}
