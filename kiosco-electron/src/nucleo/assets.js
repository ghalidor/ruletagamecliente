/* ============================================================================
   Carga de imagenes y audio.
   Todo se precarga antes del primer dibujo: si una pieza llega tarde, la
   rueda aparece incompleta durante unos frames.
   ============================================================================ */

/** Piezas de la rueda. Los nombres son los del sistema original. */
export const PIEZAS = {
    ruleta:        'ruleta.png',
    borderuleta:   'borderuleta.png',
    sombratajadas: 'sombratajadas.png',
    spin:          'spin.png',
    spinborde:     'spinborde.png',
    puntoNegro:    'circulonegrotajadas.png',
    puntoSombra:   'circulonegrotajadassombra.png',
    flecha:        'flechacirculo.png',
    luz1:          'bordeluz1.png',
    luz2:          'bordeluz2.png',
    luz3:          'bordeluz3.png'
};

// Las rutas se resuelven desde index.html, que vive en src/pantallas/.
// Por eso el ../ : sin el, el navegador busca en src/pantallas/assets/ y no
// encuentra nada, asi que la rueda se dibuja sin ninguna de sus piezas.
const RUTA_IMAGENES = '../assets/imagenes/';
const RUTA_AUDIO    = '../assets/audio/';

/**
 * Carga una imagen. Nunca rechaza: si un archivo falta, devuelve null y el
 * dibujo lo omite. Es preferible una rueda sin una pieza que una pantalla
 * negra en el pedestal.
 */
function cargarImagen(url) {
    return new Promise(resolver => {
        const imagen = new Image();
        imagen.onload  = () => resolver(imagen);
        imagen.onerror = () => {
            console.warn('No se pudo cargar la imagen:', url);
            resolver(null);
        };
        imagen.src = url;
    });
}

/** Carga todas las piezas de la rueda. */
export async function cargarPiezas() {
    const nombres = Object.keys(PIEZAS);

    const cargadas = await Promise.all(
        nombres.map(nombre => cargarImagen(RUTA_IMAGENES + PIEZAS[nombre])));

    const piezas = {};
    nombres.forEach((nombre, i) => { piezas[nombre] = cargadas[i]; });

    piezas.faltantes = nombres.filter((_, i) => cargadas[i] === null);

    if (piezas.faltantes.length === nombres.length) {
        console.error(
            'No cargo NINGUNA imagen. Revisa que los PNG esten en ' +
            'kiosco-electron/src/assets/imagenes/ con los nombres exactos.');
    } else if (piezas.faltantes.length > 0) {
        console.warn('Faltan estas piezas:',
            piezas.faltantes.map(n => PIEZAS[n]).join(', '));
    }

    return piezas;
}

/**
 * Carga las imagenes de los premios de tipo producto.
 * Devuelve un mapa tajadaId -> Image.
 */
export async function cargarImagenesPremios(tajadas, servidor) {
    const conImagen = tajadas.filter(t => t.imagen);
    if (conImagen.length === 0) return {};

    const cargadas = await Promise.all(
        conImagen.map(t => cargarImagen(servidor + t.imagen)));

    const mapa = {};
    conImagen.forEach((t, i) => {
        if (cargadas[i]) mapa[t.tajadaId] = cargadas[i];
    });

    return mapa;
}

/* --------------------------------------------------------------- Audio --- */

/**
 * Reproductor sencillo.
 *
 * El sistema original hacia el loop del giro con un truco: cuando currentTime
 * pasaba 28.7 lo devolvia a 28.4, lo que producia un clic audible en cada
 * vuelta. Aca se usa el loop nativo del elemento, que no corta.
 *
 * Ademas, alli el sonido de premio se llamaba con play() y pause() en la misma
 * linea, asi que nunca se escuchaba.
 */
export class Sonido {
    constructor(archivo, { loop = false, volumen = 1 } = {}) {
        this.audio = new Audio(RUTA_AUDIO + archivo);
        this.audio.loop = loop;
        this.audio.volume = volumen;

        // Se guarda el volumen nominal: al atenuar hay que poder volver a el.
        this.volumenBase = volumen;
        this.disponible = true;

        this.audio.addEventListener('error', () => {
            console.warn('No se pudo cargar el audio:', archivo);
            this.disponible = false;
        });
    }

    reproducir() {
        if (!this.disponible) return;
        this.audio.currentTime = 0;
        this.audio.volume = this.volumenBase;
        this.audio.play().catch(() => { /* el pedestal no debe caerse por audio */ });
    }

    /**
     * Ajusta el volumen como fraccion del nominal.
     * Permite atenuar el sonido siguiendo la animacion, en vez de cortarlo
     * despues con un desvanecido que suena a retraso.
     */
    atenuar(fraccion) {
        if (!this.disponible) return;
        this.audio.volume = Math.max(0, Math.min(1, this.volumenBase * fraccion));
    }

    detener() {
        if (!this.disponible) return;
        this.audio.pause();
        this.audio.currentTime = 0;
        this.audio.volume = this.volumenBase;
    }

    /** Baja el volumen progresivamente. Evita el corte seco al frenar la rueda. */
    desvanecer(ms = 400) {
        if (!this.disponible) return;

        const inicial = this.audio.volume;
        const arranque = performance.now();

        const paso = ahora => {
            const avance = Math.min((ahora - arranque) / ms, 1);
            this.audio.volume = inicial * (1 - avance);

            if (avance < 1) {
                requestAnimationFrame(paso);
            } else {
                this.detener();
            }
        };

        requestAnimationFrame(paso);
    }
}