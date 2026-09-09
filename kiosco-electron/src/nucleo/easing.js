/* ============================================================================
   Curvas de aceleracion.
   ============================================================================ */

/**
 * Desaceleracion fuerte al final. Es la misma curva del sistema original
 * (easeOutQuart de Penner), que da la sensacion de rueda pesada que se frena.
 * @param {number} t avance normalizado, de 0 a 1
 */
export function salidaCuartica(t) {
    const u = t - 1;
    return 1 - u * u * u * u;
}

/** Arranque y frenado suaves. Para transiciones de interfaz. */
export function entradaSalida(t) {
    return t < 0.5
        ? 4 * t * t * t
        : 1 - Math.pow(-2 * t + 2, 3) / 2;
}
