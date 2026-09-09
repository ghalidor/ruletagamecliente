namespace RuletaGame.Domain.Entities;

/// <summary>
/// Excepcion del tope para una fecha puntual. Si no hay fila para el dia,
/// manda el valor base de Configuracion (TOPE_DIARIO_MONTO).
/// </summary>
public sealed class TopeDiario
{
    public DateTime  Fecha         { get; set; }
    public decimal   Monto         { get; set; }
    public string?   Nota          { get; set; }
    public int?      UsuarioId     { get; set; }
    public DateTime  FechaCreacion { get; set; }
}
