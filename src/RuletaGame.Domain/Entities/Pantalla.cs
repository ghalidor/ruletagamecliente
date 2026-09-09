namespace RuletaGame.Domain.Entities;

public sealed class Pantalla
{
    public int       PantallaId        { get; set; }
    public string    Codigo            { get; set; } = "";
    public string    Nombre            { get; set; } = "";
    public int?      SalaId            { get; set; }
    public int?      RuletaAsignadaId  { get; set; }

    /// <summary>PEDESTAL o WEB. Ver <see cref="Enums.TipoTerminal"/>.</summary>
    public string    Tipo              { get; set; } = Enums.TipoTerminal.Pedestal;

    /// <summary>Clave de acceso de las terminales web. Nunca en claro.</summary>
    public string?   ClaveHash         { get; set; }

    /// <summary>
    /// Tope propio de esta terminal. Si tiene valor, REEMPLAZA al general:
    /// solo se acumula lo que esta terminal entrego.
    /// </summary>
    public decimal?  TopeMonto         { get; set; }
    public DateTime? UltimaConexion    { get; set; }
    public bool      Activo            { get; set; }
    public DateTime  FechaCreacion     { get; set; }
    public DateTime? FechaModificacion { get; set; }

    // Datos de apoyo para el listado
    public string? NombreRuleta { get; set; }
    public string? NombreSala   { get; set; }

    /// <summary>
    /// Viva si dio senal hace poco. Al pedestal se le exige mucho mas seguido
    /// porque manda latido constante; una terminal web puede estar cerrada y
    /// eso no significa que este rota.
    /// </summary>
    public bool EnLinea => UltimaConexion.HasValue
        && (DateTime.Now - UltimaConexion.Value).TotalSeconds
           < (EsWeb ? 600 : 90);

    public bool EsWeb      => Tipo == Enums.TipoTerminal.Web;
    public bool EsPedestal => Tipo == Enums.TipoTerminal.Pedestal;
    public bool TieneTopePropio => TopeMonto.HasValue;
}
