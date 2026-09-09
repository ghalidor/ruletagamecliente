/* ============================================================================
   Bucle de animacion.

   El sistema original corria a 15 fps con un limitador manual sobre Date.now()
   y contaba frames en vez de tiempo: la duracion real terminaba siendo el
   valor configurado por 4, sin que nadie lo supiera.

   Aca el avance se calcula con el timestamp que entrega requestAnimationFrame,
   asi que la duracion en milisegundos es exacta y va a 60 fps.
   ============================================================================ */

import { salidaCuartica } from './easing.js';

/** Lleva cualquier angulo en grados al rango [0, 360). */
function normalizar(grados) {
    return ((grados % 360) + 360) % 360;
}

export class Animador {
    constructor(rueda) {
        this.rueda    = rueda;
        this.girando  = false;
        this.anguloActual = 0;
        this.handle   = null;
    }

    /**
     * Gira hasta el angulo indicado por el servidor.
     * @param {number} anguloFinalGrados angulo total, ya incluye las vueltas
     * @param {number} duracionMs duracion real del giro
     * @param {object} eventos { alAvanzar, alTerminar }
     */
    girar(anguloFinalGrados, duracionMs, { alAvanzar, alTerminar } = {}) {
        if (this.girando) return false;

        this.girando = true;

        const anguloInicio = this.anguloActual;

        /* El servidor calcula un angulo ABSOLUTO: supone que la rueda arranca
           en cero. Pero en reposo la rueda gira despacio, asi que cuando llega
           la orden ya no esta en cero.

           Si se sumara el angulo del servidor a la posicion actual, la rueda
           frenaria desplazada y el puntero caeria en una tajada distinta a la
           que anuncia el cartel.

           Asi que se traduce a un recorrido relativo que termina en la misma
           posicion final: se conservan las vueltas completas y se calcula
           cuanto falta desde donde esta hasta el objetivo. */
        const gradosInicio = normalizar(anguloInicio * 180 / Math.PI);
        const gradosObjetivo = normalizar(anguloFinalGrados);
        const vueltas = Math.floor(anguloFinalGrados / 360);

        let faltante = gradosObjetivo - gradosInicio;
        if (faltante < 0) faltante += 360;   // siempre hacia adelante

        const recorridoGrados = vueltas * 360 + faltante;
        const recorrido = recorridoGrados * (Math.PI / 180);

        let arranque = null;

        const paso = ahora => {
            if (arranque === null) arranque = ahora;

            const avance = Math.min((ahora - arranque) / duracionMs, 1);
            this.anguloActual = anguloInicio + recorrido * salidaCuartica(avance);

            this.rueda.dibujar(this.anguloActual, true);
            alAvanzar?.(avance);

            if (avance < 1) {
                this.handle = requestAnimationFrame(paso);
            } else {
                this.girando = false;
                this.handle = null;
                alTerminar?.(this.rueda.indiceGanador());
            }
        };

        this.handle = requestAnimationFrame(paso);
        return true;
    }

    /**
     * Giro lento continuo para la pantalla de espera.
     * Un totem quieto no invita a jugar.
     */
    iniciarReposo(gradosPorSegundo = 6) {
        if (this.girando) return;

        let anterior = null;

        const paso = ahora => {
            if (this.girando) return;   // llego un giro real: cede el control

            if (anterior !== null) {
                const delta = (ahora - anterior) / 1000;
                this.anguloActual += gradosPorSegundo * delta * (Math.PI / 180);
            }
            anterior = ahora;

            this.rueda.dibujar(this.anguloActual, true);
            this.handleReposo = requestAnimationFrame(paso);
        };

        this.handleReposo = requestAnimationFrame(paso);
    }

    detenerReposo() {
        if (this.handleReposo) {
            cancelAnimationFrame(this.handleReposo);
            this.handleReposo = null;
        }
    }

    detener() {
        this.detenerReposo();
        if (this.handle) {
            cancelAnimationFrame(this.handle);
            this.handle = null;
        }
        this.girando = false;
    }

    /** Repinta sin animar. Para el resize. */
    repintar() {
        this.rueda.dibujar(this.anguloActual, false);
    }
}