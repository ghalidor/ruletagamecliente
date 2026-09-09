namespace RuletaGame.Domain.Enums;

public enum TipoTajada : byte
{
    /// <summary>Premio en dinero. Se dibuja como {simbolo}{monto}.</summary>
    Monto = 0,

    /// <summary>Premio fisico. Se dibuja la descripcion y su imagen.</summary>
    Producto = 1
}
