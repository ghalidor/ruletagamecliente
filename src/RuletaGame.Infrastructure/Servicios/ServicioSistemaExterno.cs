using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RuletaGame.Domain.Entities;

namespace RuletaGame.Infrastructure.Servicios;

public sealed class OpcionesSistemaExterno {
    public const string Seccion = "SistemaExterno";

    /// <summary>URL de registro de clientes. Vacia = envio deshabilitado.</summary>
    public string Url { get; set; } = "";

    /// <summary>
    /// Codigo de sala del otro sistema. Numerico: el contrato lo espera como
    /// numero, y mandarlo entre comillas hace que llegue en null.
    /// </summary>
    public int CodSala { get; set; }

    /// <summary>Identifica de donde viene el registro en el otro sistema.</summary>
    public string TipoRegistro { get; set; } = "RULETA GAME";

    public int TimeoutSegundos { get; set; } = 10;
}

/// <summary>Resultado de un intento de envio.</summary>
public sealed record ResultadoEnvio(
    string Estado,
    int? CodigoHttp,
    string? Mensaje,
    string PeticionJson,
    string? RespuestaJson);

/// <summary>
/// Manda los datos del cliente al sistema externo.
///
/// Distingue tres respuestas y cada una se trata distinto:
///
///   200  quedo registrado
///   409  el otro sistema ya lo tenia. NO es un fallo: reintentarlo solo
///        generaria ruido, asi que se marca como duplicado y se cierra
///   otro  fallo real, se reintenta con espera creciente
///
/// Nunca lanza: el registro local ya ocurrio y no puede deshacerse porque un
/// servicio externo no responda.
/// </summary>
public sealed class ServicioSistemaExterno(
    HttpClient cliente,
    ILogger<ServicioSistemaExterno> registro) {
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public async Task<ResultadoEnvio> EnviarAsync(
        Cliente datos, OpcionesSistemaExterno opciones, int tipoDocumento) {
        // Los nombres son EXACTOS a los del contrato del otro sistema. Si uno
        // no coincide, el deserializador lo deja en null sin avisar: el
        // servicio responde 200 y guarda un registro vacio.
        var peticion = new {
            codSala = opciones.CodSala,
            nombres = datos.Nombres,
            apellidoPaterno = datos.ApellidoPaterno,
            apellidoMaterno = datos.ApellidoMaterno ?? "",
            correo = datos.Correo ?? "",
            celular = datos.Telefono ?? "",
            idTipoDocumento = tipoDocumento,
            numeroDocumento = datos.NumeroDocumento,

            // El formulario del kiosco no lo pide: se manda vacio a proposito.
            genero = "",

            tipoRegistro = opciones.TipoRegistro,

            enviaNotificacionWhatsapp = datos.AceptaWhatsapp,
            enviaNotificacionSms = datos.AceptaSms,
            enviaNotificacionEmail = datos.AceptaEmail,
            llamadaCelular = datos.AceptaLlamada
        };

        string peticionJson = JsonSerializer.Serialize(peticion, Json);

        if(string.IsNullOrWhiteSpace(opciones.Url))
            return new ResultadoEnvio(EstadoEnvio.Descartado, null,
                "No hay URL configurada para el sistema externo.", peticionJson, null);

        try {
            using var cancelacion = new CancellationTokenSource(
                TimeSpan.FromSeconds(opciones.TimeoutSegundos));

            var respuesta = await cliente.PostAsJsonAsync(opciones.Url, peticion, cancelacion.Token);
            string cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion.Token);

            int codigo = (int)respuesta.StatusCode;

            // 409: ya lo tenian. Cerrado y sin reintentos.
            if(respuesta.StatusCode == HttpStatusCode.Conflict)
                return new ResultadoEnvio(EstadoEnvio.Duplicado, codigo,
                    "El sistema externo ya tenia este documento.", peticionJson, cuerpo);

            if(respuesta.IsSuccessStatusCode)
                return new ResultadoEnvio(EstadoEnvio.Enviado, codigo,
                    "Registrado en el sistema externo.", peticionJson, cuerpo);

            registro.LogWarning("El sistema externo respondio {Codigo} para {Doc}",
                codigo, datos.NumeroDocumento);

            return new ResultadoEnvio(EstadoEnvio.Fallido, codigo,
                Recortar(cuerpo), peticionJson, cuerpo);
        } catch(TaskCanceledException) {
            return new ResultadoEnvio(EstadoEnvio.Fallido, null,
                $"El sistema externo no respondio en {opciones.TimeoutSegundos}s.",
                peticionJson, null);
        } catch(Exception ex) {
            registro.LogWarning(ex, "Fallo el envio de {Doc}", datos.NumeroDocumento);

            return new ResultadoEnvio(EstadoEnvio.Fallido, null,
                Recortar(ex.Message), peticionJson, null);
        }
    }

    /// <summary>La columna Mensaje admite 500; un HTML de error puede traer miles.</summary>
    private static string Recortar(string? texto) {
        texto = (texto ?? "").Trim();
        return texto.Length <= 480 ? texto : texto[..480] + "...";
    }
}