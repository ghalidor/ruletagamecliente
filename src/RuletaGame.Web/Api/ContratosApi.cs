namespace RuletaGame.Web.Api;

// Contratos de entrada de la API. Records para que sean inmutables y cortos.

public sealed record LoginRequest(string Usuario, string Password);
public sealed record GirarRequest(int RuletaId, string? CodigoPantalla);
public sealed record RegistrarClienteRequest(
    long    JugadaId,
    int     TipoDocumento,
    string  NumeroDocumento,
    string  Nombres,
    string  ApellidoPaterno,
    string? ApellidoMaterno,
    string? Correo,
    string? Telefono,

    // Canales autorizados. Los cuatro en false = marco "No autorizo".
    bool    AceptaWhatsapp,
    bool    AceptaSms,
    bool    AceptaLlamada,
    bool    AceptaEmail);
public sealed record AsignarRuletaRequest(int PantallaId, int? RuletaId);
public sealed record LatidoRequest(string CodigoPantalla);
