using RuletaGame.Application.Abstractions;
using RuletaGame.Application.Common;
using RuletaGame.Application.Dtos;
using RuletaGame.Application.Servicios;
using RuletaGame.Domain.Enums;

namespace RuletaGame.Application.Queries;

public sealed record ObtenerResumenQuery;

public sealed class ObtenerResumenQueryHandler(
    IRuletaRepositorio ruletas,
    IJugadaRepositorio jugadas,
    IPantallaRepositorio pantallas,
    IConfiguracionRepositorio configuracion,
    TopeService topeServicio)
{
    public async Task<ResumenDto> EjecutarAsync()
    {
        var listaRuletas = await ruletas.ListarAsync();
        var listaPantallas = await pantallas.ListarAsync();
        var config = await configuracion.ObtenerDiccionarioAsync();

        // El tope es global: una sola cifra para todas las ruletas del dia.
        var tope = await topeServicio.ObtenerHoyAsync();

        return new ResumenDto
        {
            RuletasActivas = listaRuletas.Count(r => r.Activo),
            JugadasHoy = await jugadas.ContarHoyAsync(null),
            EntregadoHoy = await jugadas.MontoEntregadoHoyAsync(),
            ConsumoTope = tope.Consumido,
            TopeDiario = tope.Monto,
            // El tope ahora tiene cuatro origenes, no solo "general o excepcion".
            // Aca solo interesa si el de hoy difiere del base.
            TopeEsExcepcion = tope.Origen != OrigenTope.General,
            TopeNota = tope.Nota,
            MonedaSimbolo = config.LeerTexto(ClaveConfiguracion.MonedaSimbolo, "S/."),
            PantallasEnLinea = listaPantallas.Count(p => p.EnLinea),
            PantallasTotales = listaPantallas.Count,
            Pantallas = listaPantallas.Select(p => new PantallaResumenDto
            {
                PantallaId = p.PantallaId,
                Codigo = p.Codigo,
                Nombre = p.Nombre,
                NombreRuleta = p.NombreRuleta,
                EnLinea = p.EnLinea
            }).ToList()
        };
    }
}