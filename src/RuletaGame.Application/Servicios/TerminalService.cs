using System.Security.Cryptography;
using System.Text;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;

namespace RuletaGame.Application.Servicios;

/// <summary>
/// Todo lo propio de las terminales: como se identifican y como se detecta que
/// la rueda que tienen en pantalla ya no es la vigente.
/// </summary>
public sealed class TerminalService(
    IPantallaRepositorio pantallas,
    ITajadaRepositorio   tajadas,
    IHasheadorPassword   hasheador)
{
    /// <summary>
    /// Busca la terminal web cuya clave coincide.
    ///
    /// Hay que recorrerlas todas porque la clave esta hasheada con salt: no se
    /// puede buscar por igualdad. Con una decena de terminales el costo es
    /// irrelevante, y a cambio la clave nunca se guarda en claro.
    /// </summary>
    public async Task<Pantalla?> AutenticarAsync(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave)) return null;

        clave = clave.Trim();

        foreach (var terminal in await pantallas.ListarWebActivasAsync())
        {
            if (terminal.ClaveHash is not null &&
                hasheador.Verificar(clave, terminal.ClaveHash))
                return terminal;
        }

        return null;
    }

    public string HashearClave(string clave) => hasheador.Hashear(clave.Trim());

    /// <summary>
    /// Huella de la configuracion visible de una ruleta.
    ///
    /// El angulo que devuelve el servidor no dice "la tajada del televisor":
    /// dice "para 7 tajadas, la de la posicion 3". Si el kiosco dibujo 7 y el
    /// servidor calcula con 8, ese mismo angulo apunta a otro sector y la
    /// rueda frena en un premio distinto al que anuncia.
    ///
    /// Con esta huella el servidor puede negarse a sortear cuando la terminal
    /// tiene datos viejos, en vez de descubrir el desajuste cuando ya es tarde.
    ///
    /// Entra todo lo que cambia el dibujo o el reparto de sectores: cuales
    /// tajadas, en que orden y con que texto. La probabilidad NO entra: cambia
    /// quien gana, no donde esta dibujado.
    /// </summary>
    public async Task<string> CalcularVersionAsync(int ruletaId)
    {
        var activas = await tajadas.ListarPorRuletaAsync(ruletaId, soloActivas: true);

        var texto = new StringBuilder();
        texto.Append(ruletaId).Append('|');

        foreach (var t in activas)
        {
            texto.Append(t.TajadaId).Append(':')
                 .Append(t.Orden).Append(':')
                 .Append((int)t.Tipo).Append(':')
                 .Append(t.Monto.ToString("0.##")).Append(':')
                 .Append(t.Descripcion).Append(':')
                 .Append(t.Imagen ?? "").Append(';');
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString()));

        // Doce caracteres alcanzan: no es un mecanismo de seguridad, solo hace
        // falta que dos configuraciones distintas den huellas distintas.
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    /// <summary>True si la terminal esta trabajando con una rueda desactualizada.</summary>
    public async Task<bool> VersionDesactualizadaAsync(int ruletaId, string? versionTerminal)
    {
        // Sin version es una terminal vieja o el primer giro: se deja pasar
        // para no romper el pedestal que todavia no la manda.
        if (string.IsNullOrWhiteSpace(versionTerminal)) return false;

        return !string.Equals(
            versionTerminal.Trim(),
            await CalcularVersionAsync(ruletaId),
            StringComparison.OrdinalIgnoreCase);
    }
}
