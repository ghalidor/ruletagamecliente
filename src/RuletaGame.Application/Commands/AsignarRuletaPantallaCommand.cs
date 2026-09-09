using RuletaGame.Application.Abstractions;
using RuletaGame.Application.Common;

namespace RuletaGame.Application.Commands;

public sealed record AsignarRuletaPantallaCommand(int PantallaId, int? RuletaId);

/// <summary>
/// La tablet decide que ruleta queda cargada en el pedestal.
/// El valor se guarda en la base (fuente de verdad, sobrevive reinicios) y
/// ademas se empuja por WebSocket para que el cambio se vea de inmediato.
/// </summary>
public sealed class AsignarRuletaPantallaCommandHandler(
    IPantallaRepositorio pantallas,
    IRuletaRepositorio   ruletas,
    INotificadorKiosco   notificador)
{
    public async Task<Resultado> EjecutarAsync(AsignarRuletaPantallaCommand comando)
    {
        var pantalla = await pantallas.ObtenerAsync(comando.PantallaId);
        if (pantalla is null) return Resultado.Fallo("La pantalla no existe.");

        if (comando.RuletaId.HasValue)
        {
            var ruleta = await ruletas.ObtenerAsync(comando.RuletaId.Value);
            if (ruleta is null)
                return Resultado.Fallo("La ruleta no existe.");
            if (!ruleta.Activo)
                return Resultado.Fallo("No se puede dejar en el pedestal una ruleta deshabilitada.");
            if (ruleta.TajadasActivas == 0)
                return Resultado.Fallo("La ruleta no tiene premios activos.");
        }

        await pantallas.AsignarRuletaAsync(comando.PantallaId, comando.RuletaId);
        await notificador.CambiarRuletaAsync(pantalla.Codigo, comando.RuletaId);

        return Resultado.Ok(comando.RuletaId.HasValue
            ? "Ruleta enviada al pedestal."
            : "El pedestal queda sin ruleta.");
    }
}
