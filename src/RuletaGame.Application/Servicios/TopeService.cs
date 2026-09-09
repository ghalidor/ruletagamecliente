using RuletaGame.Application.Abstractions;
using RuletaGame.Application.Common;
using RuletaGame.Domain.Enums;

namespace RuletaGame.Application.Servicios;

/// <summary>De donde salio el tope aplicado. Sirve para poder explicarlo.</summary>
public enum OrigenTope
{
    General,
    ExcepcionGeneral,
    Terminal,
    ExcepcionTerminal
}

public sealed record EstadoTope(
    DateTime Fecha,
    decimal Monto,
    decimal Consumido,
    OrigenTope Origen,
    string? Nota,
    int? PantallaId)
{
    public bool SinLimite => Monto <= 0m;
    public decimal Restante => SinLimite ? decimal.MaxValue : Math.Max(0m, Monto - Consumido);
    public bool Alcanzado => !SinLimite && Consumido >= Monto;

    public int PorcentajeUsado => SinLimite ? 0 : (int)Math.Min(100m, Consumido / Monto * 100m);

    /// <summary>True cuando el tope pertenece a una terminal y no al global.</summary>
    public bool EsDeTerminal => Origen is OrigenTope.Terminal or OrigenTope.ExcepcionTerminal;

    public string Descripcion => Origen switch
    {
        OrigenTope.ExcepcionTerminal => "Excepcion de hoy de esta terminal",
        OrigenTope.Terminal => "Tope propio de la terminal",
        OrigenTope.ExcepcionGeneral => "Excepcion general de hoy",
        _ => "Tope general"
    };
}

/// <summary>
/// Resuelve cuanto se puede repartir, y contra que se acumula.
///
/// La cascada, del mas especifico al mas general:
///
///   1. Excepcion de hoy de la terminal
///   2. Tope propio de la terminal
///   3. Excepcion general de hoy
///   4. Tope general
///
/// El primero que exista, gana. Y algo que importa tanto como el monto: si el
/// tope sale de la terminal, solo se acumula lo que ESA terminal entrego. El
/// tope individual REEMPLAZA al general, no se suma con el.
/// </summary>
public sealed class TopeService(
    IJugadaRepositorio jugadas,
    ITopeDiarioRepositorio topesGenerales,
    ITopePantallaRepositorio topesPantalla,
    IPantallaRepositorio pantallas,
    IConfiguracionRepositorio configuracion)
{
    /// <param name="pantallaId">
    /// Terminal desde la que se juega. Si es null se evalua el tope general,
    /// que es lo que corresponde al tablero del gestor.
    /// </param>
    public async Task<EstadoTope> ObtenerHoyAsync(int? pantallaId = null)
    {
        DateTime hoy = DateTime.Today;

        var resuelto = await ResolverAsync(hoy, pantallaId);

        // El alcance del consumo lo decide el origen: si el tope es de la
        // terminal, se mide solo su gasto; si es general, el de todos.
        decimal consumido = resuelto.Origen is OrigenTope.Terminal or OrigenTope.ExcepcionTerminal
            ? await jugadas.MontoQueDescuentaHoyPorPantallaAsync(pantallaId!.Value)
            : await jugadas.MontoQueDescuentaHoyAsync();

        return new EstadoTope(
            Fecha: hoy,
            Monto: resuelto.Monto,
            Consumido: consumido,
            Origen: resuelto.Origen,
            Nota: resuelto.Nota,
            PantallaId: resuelto.Origen is OrigenTope.Terminal or OrigenTope.ExcepcionTerminal
                            ? pantallaId : null);
    }

    private async Task<(decimal Monto, OrigenTope Origen, string? Nota)> ResolverAsync(
        DateTime hoy, int? pantallaId)
    {
        if(pantallaId.HasValue)
        {
            // 1. Excepcion de hoy de esta terminal
            var excepcion = await topesPantalla.ObtenerAsync(pantallaId.Value, hoy);
            if(excepcion is not null)
                return (excepcion.Monto, OrigenTope.ExcepcionTerminal, excepcion.Nota);

            // 2. Tope propio de la terminal
            var pantalla = await pantallas.ObtenerAsync(pantallaId.Value);
            if(pantalla?.TopeMonto is { } propio)
                return (propio, OrigenTope.Terminal, null);
        }

        // 3. Excepcion general de hoy
        var general = await topesGenerales.ObtenerPorFechaAsync(hoy);
        if(general is not null)
            return (general.Monto, OrigenTope.ExcepcionGeneral, general.Nota);

        // 4. Tope general
        var config = await configuracion.ObtenerDiccionarioAsync();
        return (config.LeerDecimal(ClaveConfiguracion.TopeDiarioMonto, 0m),
                OrigenTope.General, null);
    }
}
