namespace RuletaGame.Domain.Entities;

/// <summary>Estados por los que pasa un envio al sistema externo.</summary>
public static class EstadoEnvio
{
    public const string Pendiente  = "PENDIENTE";
    public const string Enviado    = "ENVIADO";

    /// <summary>El otro sistema ya lo tenia. No es un fallo: no se reintenta.</summary>
    public const string Duplicado  = "DUPLICADO";

    public const string Fallido    = "FALLIDO";
    public const string Descartado = "DESCARTADO";
}

/// <summary>
/// Un intento de mandar un cliente al sistema externo.
///
/// Se guarda la peticion y la respuesta completas: si el otro equipo dice que
/// no le llego, esto es la prueba de que se envio y de como contestaron.
/// </summary>
public sealed class EnvioCliente
{
    public long      EnvioId        { get; set; }
    public int       ClienteId      { get; set; }
    public long?     JugadaId       { get; set; }

    public string    Estado         { get; set; } = EstadoEnvio.Pendiente;
    public int       Intentos       { get; set; }
    public DateTime? ProximoIntento { get; set; }

    public string?   PeticionJson   { get; set; }
    public string?   RespuestaJson  { get; set; }
    public int?      CodigoHttp     { get; set; }
    public string?   Mensaje        { get; set; }

    public DateTime  FechaCreacion  { get; set; }
    public DateTime? FechaEnvio     { get; set; }

    // Datos de apoyo para la vista de seguimiento
    public string? NumeroDocumento { get; set; }
    public string? NombreCompleto  { get; set; }

    public bool EsFinal => Estado is EstadoEnvio.Enviado
                                  or EstadoEnvio.Duplicado
                                  or EstadoEnvio.Descartado;
}
