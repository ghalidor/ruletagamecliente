using RuletaGame.Domain.Enums;

namespace RuletaGame.Domain.Entities;

public sealed class Cliente
{
    public int           ClienteId         { get; set; }
    public TipoDocumento TipoDocumento     { get; set; } = TipoDocumento.Dni;
    public string        NumeroDocumento   { get; set; } = "";

    public string        Nombres           { get; set; } = "";
    public string        ApellidoPaterno   { get; set; } = "";
    public string?       ApellidoMaterno   { get; set; }
    public string?       Correo            { get; set; }
    public string?       Telefono          { get; set; }

    /// <summary>Autorizo al menos un canal. Se deriva de los cuatro de abajo.</summary>
    public bool          AceptaUsoDatos    { get; set; }
    public DateTime?     FechaAceptacion   { get; set; }

    // La ley pide saber POR QUE MEDIO autorizo, no solo que autorizo.
    public bool          AceptaWhatsapp    { get; set; }
    public bool          AceptaSms         { get; set; }
    public bool          AceptaLlamada     { get; set; }
    public bool          AceptaEmail       { get; set; }

    /// <summary>True si no autorizo ningun canal.</summary>
    public bool SinAutorizacion =>
        !AceptaWhatsapp && !AceptaSms && !AceptaLlamada && !AceptaEmail;

    public DateTime      FechaCreacion     { get; set; }
    public DateTime?     FechaModificacion { get; set; }

    public string NombreCompleto =>
        $"{ApellidoPaterno} {ApellidoMaterno} {Nombres}".Replace("  ", " ").Trim();
}
