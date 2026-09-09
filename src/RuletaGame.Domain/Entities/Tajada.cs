using RuletaGame.Domain.Enums;

namespace RuletaGame.Domain.Entities;

public sealed class Tajada
{
    public int         TajadaId          { get; set; }
    public int         RuletaId          { get; set; }
    public string      Descripcion       { get; set; } = "";
    public TipoTajada  Tipo              { get; set; }
    public decimal     Monto             { get; set; }
    public decimal?    Probabilidad      { get; set; }
    public string?     Imagen            { get; set; }
    public int         Orden             { get; set; }
    public bool        Activo            { get; set; }

    /// <summary>
    /// Si es false, el premio se entrega pero no descuenta del tope diario.
    /// Pensado para los productos: un llavero no deberia consumir el
    /// presupuesto de efectivo del dia.
    /// </summary>
    public bool        AfectaTope        { get; set; } = true;

    public DateTime    FechaCreacion     { get; set; }
    public DateTime?   FechaModificacion { get; set; }

    /// <summary>Paleta de la rueda fisica. El color sale del orden.</summary>
    private static readonly string[] Paleta =
        ["#27CC0A", "#D6A902", "#D40108", "#9604A6", "#0261DC"];

    public string Color => Paleta[Math.Abs(Orden) % Paleta.Length];

    public string TextoEnRueda(string simboloMoneda) =>
        Tipo == TipoTajada.Monto ? $"{simboloMoneda}{Monto:0.##}" : Descripcion;
}
