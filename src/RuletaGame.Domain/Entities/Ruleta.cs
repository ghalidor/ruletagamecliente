namespace RuletaGame.Domain.Entities;

public sealed class Ruleta
{
    public int       RuletaId          { get; set; }
    public string    Nombre            { get; set; } = "";
    public string?   Descripcion       { get; set; }
    public bool      Activo            { get; set; }
    public int?      UsuarioCreacionId { get; set; }
    public DateTime  FechaCreacion     { get; set; }
    public DateTime? FechaModificacion { get; set; }

    // Calculados por las consultas de listado
    public int TotalTajadas   { get; set; }
    public int TajadasActivas { get; set; }
}
