using System.Security.Cryptography;
using RuletaGame.Domain.Entities;

namespace RuletaGame.Application.Servicios;

public sealed record ResultadoSorteo(
    Tajada  Ganadora,
    decimal AnguloFinal,
    bool    FuePonderado,
    int     Candidatas,
    bool    CierraCiclo);

/// <summary>
/// Decide la tajada ganadora y el angulo al que debe frenar la rueda.
///
/// Corrige tres defectos del sistema legacy (ruleta.js):
///
/// 1. El sorteo corria en el navegador, asi que se podia cambiar desde la
///    consola del cliente. Ahora corre aca, en el servidor.
///
/// 2. rand(0, lista.length) era inclusivo en el maximo, asi que devolvia un
///    indice fuera de rango en 1 de cada N+1 giros y caia a un sorteo uniforme
///    que ignoraba los porcentajes configurados.
///
/// 3. El ganador se sorteaba sobre TODAS las tajadas y no sobre las pendientes
///    del ciclo, asi que una tajada ya premiada podia repetirse el mismo dia.
///    Las lineas que hacian lo correcto estaban comentadas en el original.
/// </summary>
public sealed class SorteoService
{
    /// <summary>Margen en grados para no frenar justo en el borde entre tajadas.</summary>
    private const decimal MargenBordeGrados = 1.5m;

    /// <param name="dibujadas">
    /// Todas las tajadas activas de la ruleta, en su orden de dibujo.
    /// Define la geometria: cuantas porciones tiene la rueda y donde esta cada una.
    /// </param>
    /// <param name="candidatas">
    /// Subconjunto que puede ganar en este giro (las que no consumio el ciclo).
    /// Si el ciclo esta deshabilitado, es igual a <paramref name="dibujadas"/>.
    /// </param>
    /// <param name="vueltas">Vueltas completas antes de frenar. Debe ser entero.</param>
    /// <param name="ultimaGanadoraId">
    /// Tajada que salio en el giro anterior. Se excluye del sorteo para que el
    /// mismo premio no aparezca dos veces seguidas, que al publico le parece
    /// que la ruleta esta trucada aunque sea perfectamente normal.
    /// </param>
    public ResultadoSorteo Sortear(
        IReadOnlyList<Tajada> dibujadas,
        IReadOnlyList<Tajada> candidatas,
        int vueltas,
        int? ultimaGanadoraId = null)
    {
        if (dibujadas.Count == 0)
            throw new InvalidOperationException("La ruleta no tiene tajadas activas.");
        if (candidatas.Count == 0)
            throw new InvalidOperationException("No quedan tajadas disponibles en el ciclo.");

        var elegibles = SinRepetirLaUltima(candidatas, ultimaGanadoraId);

        var (ganadora, ponderado) = Elegir(elegibles);

        int indice = IndiceEnRueda(dibujadas, ganadora.TajadaId);
        if (indice < 0)
            throw new InvalidOperationException(
                $"La tajada {ganadora.TajadaId} no esta entre las dibujadas.");

        decimal angulo = CalcularAnguloFinal(indice, dibujadas.Count, vueltas);

        // El ciclo se cierra cuando esta era la ultima tajada disponible.
        bool cierraCiclo = candidatas.Count == 1;

        return new ResultadoSorteo(ganadora, angulo, ponderado, candidatas.Count, cierraCiclo);
    }

    /// <summary>
    /// Quita del sorteo la tajada que acaba de salir.
    ///
    /// Con una sola tajada disponible no se aplica: la alternativa seria que
    /// la ruleta deje de funcionar, y eso es peor que repetir un premio.
    /// </summary>
    private static IReadOnlyList<Tajada> SinRepetirLaUltima(
        IReadOnlyList<Tajada> candidatas, int? ultimaGanadoraId)
    {
        if (ultimaGanadoraId is null || candidatas.Count <= 1) return candidatas;

        var restantes = candidatas.Where(t => t.TajadaId != ultimaGanadoraId).ToList();

        // Si al quitarla no queda ninguna, se deja pasar la repeticion.
        return restantes.Count > 0 ? restantes : candidatas;
    }

    /// <summary>
    /// Sorteo ponderado por probabilidad. Si ninguna candidata tiene
    /// probabilidad util, cae a uniforme, que es el comportamiento esperado
    /// cuando el operador todavia no configuro porcentajes.
    /// </summary>
    private static (Tajada Ganadora, bool Ponderado) Elegir(IReadOnlyList<Tajada> candidatas)
    {
        decimal pesoTotal = candidatas.Sum(t => t.Probabilidad ?? 0m);

        if (pesoTotal <= 0m)
            return (candidatas[Aleatorio(candidatas.Count)], false);

        // Recorrido acumulado sobre el peso real, sin listas repetidas ni el
        // desborde de indice del original.
        decimal punto     = AleatorioDecimal() * pesoTotal;
        decimal acumulado = 0m;

        foreach (var tajada in candidatas)
        {
            acumulado += tajada.Probabilidad ?? 0m;
            if (punto < acumulado) return (tajada, true);
        }

        // Solo alcanzable por redondeo decimal en el ultimo tramo.
        return (candidatas[^1], true);
    }

    private static int IndiceEnRueda(IReadOnlyList<Tajada> dibujadas, int tajadaId)
    {
        for (int i = 0; i < dibujadas.Count; i++)
            if (dibujadas[i].TajadaId == tajadaId) return i;
        return -1;
    }

    /// <summary>
    /// Angulo total de rotacion en grados.
    ///
    /// Replica la geometria del original: el puntero esta arriba y el mapeo de
    /// indice a angulo va invertido, asi que la tajada en la posicion k ocupa
    /// el sector [360*(N-k-1)/N , 360*(N-k)/N].
    ///
    /// Las vueltas completas son multiplo de 360, asi que no desplazan el
    /// resultado: solo hacen que la rueda gire varias veces antes de frenar.
    /// </summary>
    public static decimal CalcularAnguloFinal(int indice, int total, int vueltas)
    {
        decimal anchoSector = 360m / total;
        decimal desde = anchoSector * (total - indice - 1) + MargenBordeGrados;
        decimal hasta = anchoSector * (total - indice)     - MargenBordeGrados;

        // Sectores muy delgados: apunta al centro en vez de invertir los limites.
        if (desde >= hasta)
        {
            decimal centro = anchoSector * (total - indice - 0.5m);
            return vueltas * 360m + centro;
        }

        return vueltas * 360m + desde + AleatorioDecimal() * (hasta - desde);
    }

    /// <summary>Indice uniforme en [0, limite). Sin modulo sesgado.</summary>
    private static int Aleatorio(int limite) => RandomNumberGenerator.GetInt32(limite);

    /// <summary>Decimal uniforme en [0, 1).</summary>
    private static decimal AleatorioDecimal()
    {
        const int Escala = 1_000_000;
        return RandomNumberGenerator.GetInt32(Escala) / (decimal)Escala;
    }
}
