namespace RuletaGame.Domain.Entities;

public sealed class Configuracion
{
    public string    Clave             { get; set; } = "";
    public string    Valor             { get; set; } = "";
    public string    Descripcion       { get; set; } = "";
    public string    TipoDato          { get; set; } = "TEXTO";
    public DateTime? FechaModificacion { get; set; }
}
