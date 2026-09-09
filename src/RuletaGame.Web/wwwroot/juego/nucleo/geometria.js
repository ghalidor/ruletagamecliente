/* ============================================================================
   Geometria de la rueda.
   Todos los radios salen de un radio base r, igual que en el sistema original,
   para que las piezas graficas encajen sin retocarlas.
   ============================================================================ */

export const GRADOS_A_RAD = Math.PI / 180;

/** Radios derivados. Las proporciones vienen del sistema original. */
export function calcularRadios(r) {
    const rBordeSpin = r / 3;

    return {
        r,
        rBordeSpin,
        rBordeRuleta:   r * 1.215,
        rRuletaImagen:  r * 1.070,
        rSombraTajadas: r * 1.070 * 0.95,
        rSpin:          r / 3.8,
        rSombraSpin:    rBordeSpin * 1.52,
        rTajadaImagen:  rBordeSpin + (r - rBordeSpin) * 0.77,
        rCirculoFlecha: rBordeSpin * 0.3,
        rPuntosNegros:  rBordeSpin / 11,
        rPuntosSpin:    rBordeSpin / 12
    };
}

/** Radio base a partir del viewport. */
export function radioBase(ancho, alto) {
    return Math.min(ancho, alto) / 2.5 | 0;
}

/**
 * Sector que ocupa la tajada de indice i al dibujar.
 * El puntero esta arriba, por eso el desfase de -PI/2.
 */
export function sectorDibujo(indice, total) {
    const anchoGrados = 360 / total;
    const desde = (anchoGrados * indice) * GRADOS_A_RAD - Math.PI / 2;

    return { desde, hasta: desde + anchoGrados * GRADOS_A_RAD };
}

/**
 * Indice de la tajada bajo el puntero para un angulo de rotacion dado.
 * El servidor calcula el angulo con esta misma formula invertida, asi que
 * el resultado siempre coincide con el ganador que envio.
 */
export function indiceEnPuntero(anguloRad, total) {
    let indice = Math.floor((-anguloRad) * total / (2 * Math.PI)) % total;
    if (indice < 0) indice += total;
    return indice;
}
