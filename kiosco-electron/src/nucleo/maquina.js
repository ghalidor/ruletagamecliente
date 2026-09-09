/* ============================================================================
   Maquina de estados del pedestal.

   SIN_RULETA ---(la tablet asigna)---> STANDBY (bloqueada)
                                          |  ^
                            (boton JUGAR) |  | sin tocar en 15s
                                          v  |
                                        LISTO (desbloqueada)
                                          |  ^
                            (toca la rueda)  | timeout configurable
                                          v  |
                                       GIRANDO
                                          |
                                          v
                                      RESULTADO --(Continuar)--> STANDBY
                                          |
                                    (Registrarme)
                                          v
                                       REGISTRO --(guarda)--> STANDBY
   ============================================================================ */

export const Estado = {
    SIN_RULETA: 'SIN_RULETA',

    /** Rueda bloqueada. Se ve, pero no gira: hay que tocar JUGAR primero. */
    STANDBY:    'STANDBY',

    /** Desbloqueada. El cliente ya puede tocar la rueda para girarla. */
    LISTO:      'LISTO',

    GIRANDO:    'GIRANDO',
    RESULTADO:  'RESULTADO',
    /** Paso 1 del registro: tipo y numero de documento. */
    DOCUMENTO:  'DOCUMENTO',

    /** Paso 2 del registro: nombres, apellidos y contacto. */
    DATOS:      'DATOS',
    ERROR:      'ERROR'
};

/** Transiciones validas. Impide estados imposibles por un doble toque. */
const PERMITIDAS = {
    SIN_RULETA: ['STANDBY', 'ERROR'],
    // STANDBY consigo mismo: recargar la ruleta o los premios estando ya en
    // espera es una transicion valida, no un error.
    STANDBY:    ['STANDBY', 'LISTO', 'SIN_RULETA', 'ERROR'],
    // Desde LISTO se puede volver a bloquear si el cliente se fue.
    LISTO:      ['GIRANDO', 'STANDBY', 'SIN_RULETA', 'ERROR'],
    GIRANDO:    ['RESULTADO', 'ERROR'],
    RESULTADO:  ['DOCUMENTO', 'STANDBY', 'SIN_RULETA'],
    DOCUMENTO:  ['DATOS', 'RESULTADO', 'STANDBY', 'SIN_RULETA'],
    DATOS:      ['DOCUMENTO', 'STANDBY', 'SIN_RULETA'],
    ERROR:      ['STANDBY', 'SIN_RULETA']
};

export class Maquina {
    constructor(alCambiar) {
        this.estado = Estado.SIN_RULETA;
        this.alCambiar = alCambiar;
        this.temporizador = null;
    }

    puede(destino) {
        return PERMITIDAS[this.estado]?.includes(destino) ?? false;
    }

    ir(destino, datos = {}) {
        if (!this.puede(destino)) {
            console.warn(`Transicion invalida: ${this.estado} -> ${destino}`);
            return false;
        }

        this.cancelarTemporizador();

        const anterior = this.estado;
        this.estado = destino;
        this.alCambiar(destino, anterior, datos);

        return true;
    }

    /** Vuelve solo a otro estado tras N segundos. */
    programarSalida(segundos, destino) {
        this.cancelarTemporizador();
        this.temporizador = setTimeout(() => this.ir(destino), segundos * 1000);
    }

    /** Reinicia la cuenta. Se usa con cada tecla del DNI. */
    reprogramar(segundos, destino) {
        if (this.temporizador) this.programarSalida(segundos, destino);
    }

    cancelarTemporizador() {
        if (this.temporizador) {
            clearTimeout(this.temporizador);
            this.temporizador = null;
        }
    }

    es(...estados) {
        return estados.includes(this.estado);
    }
}
