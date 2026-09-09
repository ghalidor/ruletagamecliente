using RuletaGame.Application.Abstractions;
using RuletaGame.Application.Common;
using RuletaGame.Application.Dtos;
using RuletaGame.Application.Servicios;
using RuletaGame.Domain.Entities;
using RuletaGame.Domain.Enums;

namespace RuletaGame.Application.Commands;

public sealed record GirarCommand(
    int     RuletaId,
    string? CodigoPantalla,

    /// <summary>
    /// Huella de la rueda que la terminal tiene dibujada. Si no coincide con
    /// la vigente, el servidor se niega a sortear: con otra cantidad de
    /// tajadas el angulo apuntaria a un sector distinto.
    /// </summary>
    string? VersionRueda = null);

/// <summary>Motivo por el que un giro no se pudo hacer.</summary>
public enum MotivoRechazo
{
    Ninguno,
    RuedaDesactualizada,
    TopeAlcanzado,
    SinPremios,
    RuletaNoDisponible
}

/// <summary>
/// Un giro completo: valida el tope, elige el ganador, calcula el angulo y
/// guarda la jugada. Todo en una sola transaccion.
/// </summary>
public sealed class GirarCommandHandler(
    IRuletaRepositorio        ruletas,
    ITajadaRepositorio        tajadas,
    IJugadaRepositorio        jugadas,
    ICicloRepositorio         ciclos,
    IPantallaRepositorio      pantallas,
    IConfiguracionRepositorio configuracion,
    SorteoService             sorteo,
    TopeService               topeServicio,
    TerminalService           terminales,
    IDbSesion                 sesion)
{
    /// <summary>
    /// Mensaje convenido con el kiosco: al recibirlo recarga la rueda y
    /// reintenta el giro una sola vez, sin decirle nada al cliente.
    /// </summary>
    public const string RECARGAR = "RECARGAR_RULETA";

    public async Task<Resultado<GiroDto>> EjecutarAsync(GirarCommand comando)
    {
        var ruleta = await ruletas.ObtenerAsync(comando.RuletaId);
        if (ruleta is null)  return Resultado<GiroDto>.Fallo("La ruleta no existe.");
        if (!ruleta.Activo)  return Resultado<GiroDto>.Fallo("La ruleta esta deshabilitada.");

        var dibujadas = await tajadas.ListarPorRuletaAsync(comando.RuletaId, soloActivas: true);
        if (dibujadas.Count == 0)
            return Resultado<GiroDto>.Fallo("La ruleta no tiene premios activos.");

        // Antes de gastar el giro: si la terminal dibujo otra rueda, no se
        // sortea. El cliente no deberia ver nunca un premio que no esta en
        // pantalla, y detectarlo despues obligaria a mentir o a mostrar error.
        if (await terminales.VersionDesactualizadaAsync(comando.RuletaId, comando.VersionRueda))
            return Resultado<GiroDto>.Fallo(RECARGAR);

        var config = await configuracion.ObtenerDiccionarioAsync();
        int  vueltas         = config.LeerEntero(ClaveConfiguracion.GiroVueltas, 16);
        bool cicloHabilitado = config.LeerBool(ClaveConfiguracion.CicloHabilitado, true);

        // Tope diario GLOBAL, evaluado en CADA giro.
        //
        // En el sistema legacy este control vivia en el navegador del gestor y
        // solo corria al abrir la pantalla de juego: escrito como if (total) en
        // vez de if (total > 0), dejaba pasar cuando ya se habia excedido y no
        // volvia a revisar nunca mas mientras la pestana siguiera abierta.
        int? pantallaId = null;
        if (!string.IsNullOrWhiteSpace(comando.CodigoPantalla))
            pantallaId = (await pantallas.ObtenerPorCodigoAsync(comando.CodigoPantalla))?.PantallaId;

        // El tope se resuelve para ESTA terminal: si tiene uno propio, solo
        // cuenta su gasto; si no, cae al general.
        var estadoTope = await topeServicio.ObtenerHoyAsync(pantallaId);
        if (estadoTope.Alcanzado)
            return Resultado<GiroDto>.Fallo("Se alcanzo el tope de premios del dia.");

        await sesion.IniciarTransaccionAsync();
        try
        {
            // --- Ciclo: las tajadas ya premiadas no vuelven a salir ---
            Ciclo? ciclo = null;
            IReadOnlyList<Tajada> candidatas = dibujadas;

            if (cicloHabilitado)
            {
                ciclo = await ciclos.ObtenerAbiertoAsync(comando.RuletaId)
                        ?? await ciclos.AbrirAsync(comando.RuletaId);

                var consumidas = await ciclos.TajadasConsumidasAsync(ciclo.CicloId);
                candidatas = dibujadas.Where(t => !consumidas.Contains(t.TajadaId)).ToList();

                // Ciclo agotado: se cierra y se abre uno nuevo.
                if (candidatas.Count == 0)
                {
                    await ciclos.CerrarAsync(ciclo.CicloId);
                    ciclo = await ciclos.AbrirAsync(comando.RuletaId);
                    candidatas = dibujadas;
                }
            }

            // No repetir el premio anterior: al publico le parece que la
            // ruleta esta trucada aunque el azar lo permita perfectamente.
            int? ultima = await jugadas.UltimaTajadaAsync(comando.RuletaId, pantallaId);

            var resultado = sorteo.Sortear(dibujadas, candidatas, vueltas, ultima);

            var jugada = new Jugada
            {
                RuletaId             = comando.RuletaId,
                TajadaId             = resultado.Ganadora.TajadaId,
                CicloId              = ciclo?.CicloId,
                PantallaId           = pantallaId,
                DescripcionPremio    = resultado.Ganadora.Descripcion,
                TipoPremio           = resultado.Ganadora.Tipo,
                MontoPremio          = resultado.Ganadora.Monto,

                // Copia historica: si manana cambian si este premio descuenta,
                // esta jugada conserva lo que valia hoy.
                AfectaTope           = resultado.Ganadora.AfectaTope,
                ProbabilidadAplicada = resultado.Ganadora.Probabilidad,
                MontoTopeVigente     = estadoTope.SinLimite ? null : estadoTope.Monto,
                AnguloFinal          = resultado.AnguloFinal,
                SorteoPonderado      = resultado.FuePonderado,
                CandidatasEnSorteo   = resultado.Candidatas,
                Registrado           = false,
                ConsumeCiclo         = false,   // recien al registrar el DNI
                FechaJuego           = DateTime.Now
            };

            jugada.JugadaId = await jugadas.CrearAsync(jugada);

            await sesion.ConfirmarAsync();

            string simbolo = config.LeerTexto(ClaveConfiguracion.MonedaSimbolo, "S/.");

            return Resultado<GiroDto>.Ok(new GiroDto
            {
                JugadaId          = jugada.JugadaId,
                TajadaId          = resultado.Ganadora.TajadaId,
                AnguloFinal       = resultado.AnguloFinal,
                DuracionMs        = config.LeerEntero(ClaveConfiguracion.GiroDuracionMs, 8000),
                TextoPremio       = resultado.Ganadora.TextoEnRueda(simbolo),
                DescripcionPremio = resultado.Ganadora.Descripcion,
                TipoPremio        = (int)resultado.Ganadora.Tipo,
                MontoPremio       = resultado.Ganadora.Monto,
                CierraCiclo       = resultado.CierraCiclo,
                VersionRueda      = await terminales.CalcularVersionAsync(comando.RuletaId)
            });
        }
        catch
        {
            await sesion.RevertirAsync();
            throw;
        }
    }
}
