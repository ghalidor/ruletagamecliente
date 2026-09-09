using RuletaGame.Application.Abstractions;
using RuletaGame.Application.Common;
using RuletaGame.Application.Dtos;
using RuletaGame.Domain.Enums;

namespace RuletaGame.Application.Queries;

public sealed record ObtenerRuletaKioscoQuery(int RuletaId);

/// <summary>
/// Lo unico que el kiosco pide al arrancar y cuando la tablet cambia la ruleta.
/// Nunca devuelve probabilidades: el sorteo es del servidor y el cliente no
/// tiene por que conocer los pesos.
/// </summary>
public sealed class ObtenerRuletaKioscoQueryHandler(
    IRuletaRepositorio ruletas,
    ITajadaRepositorio tajadas,
    IConfiguracionRepositorio configuracion,
    Servicios.TerminalService terminales) {
    public async Task<Resultado<RuletaKioscoDto>> EjecutarAsync(ObtenerRuletaKioscoQuery consulta) {
        var ruleta = await ruletas.ObtenerAsync(consulta.RuletaId);
        if(ruleta is null || !ruleta.Activo)
            return Resultado<RuletaKioscoDto>.Fallo("Ruleta no disponible.");

        var lista = await tajadas.ListarPorRuletaAsync(consulta.RuletaId, soloActivas: true);
        var config = await configuracion.ObtenerDiccionarioAsync();
        string simbolo = config.LeerTexto(ClaveConfiguracion.MonedaSimbolo, "S/.");

        return Resultado<RuletaKioscoDto>.Ok(new RuletaKioscoDto {
            RuletaId = ruleta.RuletaId,
            Nombre = ruleta.Nombre,
            Tajadas = lista.Select(t => new TajadaKioscoDto {
                TajadaId = t.TajadaId,
                Texto = t.TextoEnRueda(simbolo),
                Descripcion = t.Descripcion,
                Tipo = (int)t.Tipo,
                Monto = t.Monto,
                Imagen = t.Imagen,
                Color = t.Color,
                Orden = t.Orden
            }).ToList(),
            MonedaSimbolo = simbolo,
            GiroDuracionMs = config.LeerEntero(ClaveConfiguracion.GiroDuracionMs, 8000),
            GiroVueltas = config.LeerEntero(ClaveConfiguracion.GiroVueltas, 16),
            StandbyTimeoutSegundos = config.LeerEntero(ClaveConfiguracion.StandbyTimeoutSegundos, 20),
            RegistroTimeoutSegundos = config.LeerEntero(ClaveConfiguracion.RegistroTimeoutSegundos, 60),
            PideDatosCliente = config.LeerBool(ClaveConfiguracion.ClientePideDatos, true),
            VersionRueda = await terminales.CalcularVersionAsync(consulta.RuletaId),
            CorreoObligatorio = config.LeerBool("CLIENTE_CORREO_OBLIGATORIO", false),
            TelefonoObligatorio = config.LeerBool("CLIENTE_TELEFONO_OBLIGATORIO", false),

            // Las reglas viajan al kiosco para dar respuesta inmediata al
            // cliente, pero la validacion que manda sigue siendo la del
            // servidor: el JavaScript se puede saltar, el handler no.
            TiposDocumento = Enum.GetValues<TipoDocumento>().Select(t => {
                var (minimo, maximo) = ReglasDocumento.Longitud(t);
                return new TipoDocumentoDto {
                    Valor = (int)t,
                    Nombre = ReglasDocumento.Nombre(t),
                    NombreCorto = ReglasDocumento.NombreCorto(t),
                    SoloNumeros = ReglasDocumento.SoloNumeros(t),
                    Minimo = minimo,
                    Maximo = maximo
                };
            }).ToList()
        });
    }
}
