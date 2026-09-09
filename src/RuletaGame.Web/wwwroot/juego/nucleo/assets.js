/* ============================================================================
   Carga de imagenes y audio para la terminal web.

   Mismo contenido que el kiosco Electron, con una sola diferencia: las rutas
   se resuelven contra la raiz del sitio, no contra el sistema de archivos.
   ============================================================================ */

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

const RUTA_IMAGENES = '/juego/assets/imagenes/';
const RUTA_AUDIO    = '/juego/assets/audio/';

/**
 * Carga una imagen. Nunca rechaza: si un archivo falta devuelve null y el
 * dibujo lo omite. Es preferible una rueda incompleta a una pantalla negra.
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
            'wwwroot/juego/assets/imagenes/ con los nombres exactos.');
    }

    return piezas;
}

/** Imagenes de los premios de tipo producto. Vienen del mismo servidor. */
export async function cargarImagenesPremios(tajadas) {
    const conImagen = tajadas.filter(t => t.imagen);
    if (conImagen.length === 0) return {};

    const cargadas = await Promise.all(conImagen.map(t => cargarImagen(t.imagen)));

    const mapa = {};
    conImagen.forEach((t, i) => { if (cargadas[i]) mapa[t.tajadaId] = cargadas[i]; });

    return mapa;
}

/* --------------------------------------------------------------- Audio --- */

/**
 * En navegador el audio no puede sonar hasta que hay un gesto del usuario.
 * Aca eso no es problema: el primer gesto es tocar JUGAR, que ocurre siempre
 * antes del giro. En el pedestal habia que forzarlo con una opcion de Electron.
 */
export class Sonido {
    constructor(archivo, { loop = false, volumen = 1 } = {}) {
        this.audio = new Audio(RUTA_AUDIO + archivo);
        this.audio.loop = loop;
        this.audio.volume = volumen;
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
        this.audio.play().catch(() => { /* sin audio la ruleta igual funciona */ });
    }

    /** Volumen como fraccion del nominal, para seguir la desaceleracion. */
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
}
