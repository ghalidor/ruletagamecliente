using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RuletaGame.Application.Abstractions;

namespace RuletaGame.Infrastructure.Servicios;

public sealed class OpcionesPadron
{
    public const string Seccion = "Padron";

    /// <summary>URL del servicio. Vacia = autocompletado deshabilitado.</summary>
    public string Url             { get; set; } = "";

    /// <summary>
    /// Segundos de espera. Corto a proposito: el cliente esta parado frente al
    /// pedestal y no puede quedarse mirando una pantalla congelada.
    /// </summary>
    public int    TimeoutSegundos { get; set; } = 4;
}

/// <summary>
/// Consulta los nombres de una persona por su documento.
///
/// Va contra un servicio externo, asi que la regla es que NUNCA rompa el
/// registro: si no responde, si tarda, o si devuelve algo raro, se comporta
/// como si no hubiera encontrado a nadie y el cliente escribe a mano. El
/// autocompletado es una comodidad, no un requisito.
///
/// El kiosco no llama al servicio directo: pasa por aca. Asi la URL vive en
/// appsettings y no en el codigo del pedestal, y el timeout se controla en un
/// solo lugar.
/// </summary>
public sealed class ServicioPadron(
    HttpClient cliente,
    ILogger<ServicioPadron> registro)
{
    public sealed record DatosPersona(string Nombres, string ApellidoPaterno, string ApellidoMaterno);

    public async Task<DatosPersona?> ConsultarAsync(string url, string numeroDocumento, int timeout)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            using var cancelacion = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

            var respuesta = await cliente.PostAsJsonAsync(
                url, new { coincidencia = numeroDocumento }, cancelacion.Token);

            if (!respuesta.IsSuccessStatusCode)
            {
                registro.LogWarning("El padron respondio {Codigo} para {Doc}",
                    (int)respuesta.StatusCode, numeroDocumento);
                return null;
            }

            var datos = await respuesta.Content.ReadFromJsonAsync<RespuestaPadron>(
                cancellationToken: cancelacion.Token);

            // Se exige coincidencia exacta del documento: el servicio busca por
            // "coincidencia" y podria devolver a otra persona con un numero
            // parecido. Rellenar con los datos equivocados es peor que no
            // rellenar nada.
            var persona = datos?.Data?.FirstOrDefault(
                p => string.Equals(p.NroDoc?.Trim(), numeroDocumento, StringComparison.Ordinal));

            if (persona is null) return null;

            return new DatosPersona(
                Limpiar(persona.Nombre),
                Limpiar(persona.ApelPat),
                Limpiar(persona.ApelMat));
        }
        catch (TaskCanceledException)
        {
            registro.LogWarning("El padron no respondio en {Timeout}s para {Doc}",
                timeout, numeroDocumento);
            return null;
        }
        catch (Exception ex)
        {
            // Cualquier fallo se traga a proposito: el registro debe seguir.
            registro.LogWarning(ex, "Fallo la consulta al padron para {Doc}", numeroDocumento);
            return null;
        }
    }

    private static string Limpiar(string? texto) => (texto ?? "").Trim();

    /// <summary>Forma de la respuesta del servicio. Solo lo que se usa.</summary>
    private sealed class RespuestaPadron
    {
        [JsonPropertyName("respuesta")] public bool Respuesta { get; set; }
        [JsonPropertyName("data")]      public List<PersonaPadron>? Data { get; set; }
    }

    private sealed class PersonaPadron
    {
        [JsonPropertyName("NroDoc")]  public string? NroDoc  { get; set; }
        [JsonPropertyName("Nombre")]  public string? Nombre  { get; set; }
        [JsonPropertyName("ApelPat")] public string? ApelPat { get; set; }
        [JsonPropertyName("ApelMat")] public string? ApelMat { get; set; }
    }
}
