using RuletaGame.Domain.Enums;

namespace RuletaGame.Domain.Entities;

/// <summary>
/// Un giro. Unifica ruletadiatope + ruletatajadaganadora del sistema legacy.
/// Se graba en el giro, no al registrar el DNI: asi queda el conteo real de
/// jugadas, que en el sistema anterior se perdia.
/// </summary>
public sealed class Jugada
{
    public long        JugadaId             { get; set; }
    public int         RuletaId             { get; set; }
    public int         TajadaId             { get; set; }
    public int?        CicloId              { get; set; }
    public int?        PantallaId           { get; set; }

    public string      DescripcionPremio    { get; set; } = "";
    public TipoTajada  TipoPremio           { get; set; }
    public decimal     MontoPremio          { get; set; }

    /// <summary>
    /// Si este premio descuenta del tope. Es una COPIA del valor que tenia la
    /// tajada al momento del giro, no una lectura del actual: cambiar hoy la
    /// marca de un premio no debe reescribir lo que paso la semana pasada.
    /// </summary>
    public bool        AfectaTope           { get; set; } = true;
    public decimal?    ProbabilidadAplicada { get; set; }
    public decimal?    MontoTopeVigente     { get; set; }

    public decimal     AnguloFinal          { get; set; }
    public bool        SorteoPonderado      { get; set; }
    public int         CandidatasEnSorteo   { get; set; }

    public string?        Dni              { get; set; }   // numero de documento
    public TipoDocumento? TipoDocumento    { get; set; }
    public int?           ClienteId        { get; set; }
    public bool           Registrado       { get; set; }
    public bool        ConsumeCiclo         { get; set; }

    public DateTime    FechaJuego           { get; set; }
    public DateTime?   FechaRegistro        { get; set; }

    // Datos de apoyo para el reporte
    public string? NombreRuleta   { get; set; }
    public string? NombrePantalla { get; set; }
    public string? NombreCliente  { get; set; }
    public string? CorreoCliente  { get; set; }

}
