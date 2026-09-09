namespace RuletaGame.Domain.Entities;

/// <summary>
/// Agrupa las jugadas premiadas hasta que se agotan las tajadas de la ruleta.
/// Reemplaza el truco legacy de anular por fecha con UPDATE ... SET TA_estado = 0.
/// </summary>
public sealed class Ciclo
{
    public int       CicloId     { get; set; }
    public int       RuletaId    { get; set; }
    public int       Numero      { get; set; }
    public DateTime  FechaInicio { get; set; }
    public DateTime? FechaCierre { get; set; }
    public bool      Cerrado     { get; set; }
}
