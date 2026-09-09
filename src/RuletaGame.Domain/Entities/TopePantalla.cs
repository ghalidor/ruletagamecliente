namespace RuletaGame.Domain.Entities;

/// <summary>
/// Excepcion de tope para una terminal en una fecha puntual.
/// Es lo mismo que TopeDiario, pero acotado a una terminal.
/// </summary>
public sealed class TopePantalla
{
    public int       PantallaId    { get; set; }
    public DateTime  Fecha         { get; set; }
    public decimal   Monto         { get; set; }
    public string?   Nota          { get; set; }
    public int?      UsuarioId     { get; set; }
    public DateTime  FechaCreacion { get; set; }
}
