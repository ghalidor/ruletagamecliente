namespace RuletaGame.Domain.Entities;

public sealed class Sala
{
    public int       SalaId            { get; set; }
    public string    Nombre            { get; set; } = "";
    public string?   Direccion         { get; set; }
    public bool      Activo            { get; set; }
    public DateTime  FechaCreacion     { get; set; }
    public DateTime? FechaModificacion { get; set; }
}
