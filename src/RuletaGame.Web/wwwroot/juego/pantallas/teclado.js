/* ============================================================================
   Teclado tactil.

   El pedestal no tiene teclado fisico y el del sistema operativo no aparece en
   modo kiosco, asi que todo lo que se escribe pasa por aca.

   Dos modos:
   - numerico: cuadricula de 12 teclas, para documento y telefono
   - texto:    QWERTY completo, para nombres y correo
   ============================================================================ */

const FILAS_TEXTO = [
    ['1','2','3','4','5','6','7','8','9','0'],
    ['q','w','e','r','t','y','u','i','o','p'],
    ['a','s','d','f','g','h','j','k','l','ñ'],
    ['z','x','c','v','b','n','m','@','.','-']
];

export class Teclado {
    /**
     * @param {HTMLElement} contenedor
     * @param {object} opciones { modo, longitudMaxima, soloNumeros, alCambiar, sonido }
     */
    constructor(contenedor, opciones = {}) {
        this.contenedor = contenedor;
        this.sonido     = opciones.sonido;

        this.modo           = opciones.modo ?? 'numerico';
        this.longitudMaxima = opciones.longitudMaxima ?? 40;
        this.soloNumeros    = opciones.soloNumeros ?? false;
        this.mayusculas     = true;

        this.alCambiar = opciones.alCambiar;

        this.valor = '';
        this.construir();
    }

    /** Cambia entre numerico y QWERTY sin recrear el objeto. */
    configurar({ modo, longitudMaxima, soloNumeros } = {}) {
        if (modo !== undefined)           this.modo = modo;
        if (longitudMaxima !== undefined) this.longitudMaxima = longitudMaxima;
        if (soloNumeros !== undefined)    this.soloNumeros = soloNumeros;

        this.construir();
    }

    construir() {
        this.contenedor.className =
            this.modo === 'numerico' ? 'teclado teclado-numerico' : 'teclado teclado-texto';

        this.contenedor.innerHTML = this.modo === 'numerico'
            ? this.plantillaNumerica()
            : this.plantillaTexto();

        // Pointer events cubre dedo y mouse con un solo camino de codigo.
        this.contenedor.onpointerdown = evento => {
            const boton = evento.target.closest('[data-tecla]');
            if (!boton) return;

            evento.preventDefault();
            this.pulsar(boton.dataset.tecla);
        };
    }

    plantillaNumerica() {
        return ['1','2','3','4','5','6','7','8','9','limpiar','0','borrar']
            .map(t => this.boton(t)).join('');
    }

    plantillaTexto() {
        const filas = FILAS_TEXTO.map((fila, indice) => {
            let botones = fila.map(t => this.boton(t)).join('');

            // A la ultima fila se le suman las teclas de control
            if (indice === FILAS_TEXTO.length - 1)
                botones = this.boton('mayus') + botones + this.boton('borrar');

            return `<div class="teclado-fila">${botones}</div>`;
        }).join('');

        return filas +
            `<div class="teclado-fila">${this.boton('espacio')}${this.boton('limpiar')}</div>`;
    }

    boton(tecla) {
        const especiales = {
            borrar:  { texto: '&#9003;', clase: 'tecla-accion',  etiqueta: 'Borrar' },
            limpiar: { texto: 'C',       clase: 'tecla-accion',  etiqueta: 'Limpiar' },
            mayus:   { texto: '&#8679;', clase: 'tecla-accion',  etiqueta: 'Mayusculas' },
            espacio: { texto: 'espacio', clase: 'tecla-espacio', etiqueta: 'Espacio' }
        };

        const especial = especiales[tecla];

        if (especial)
            return `<button class="tecla ${especial.clase}" data-tecla="${tecla}"
                            aria-label="${especial.etiqueta}">${especial.texto}</button>`;

        const visible = this.mayusculas ? tecla.toUpperCase() : tecla;
        return `<button class="tecla" data-tecla="${tecla}">${visible}</button>`;
    }

    pulsar(tecla) {
        this.sonido?.reproducir();

        switch (tecla) {
            case 'borrar':
                this.valor = this.valor.slice(0, -1);
                break;

            case 'limpiar':
                this.valor = '';
                break;

            case 'mayus':
                this.mayusculas = !this.mayusculas;
                this.construir();
                return;   // no altera el valor

            case 'espacio':
                if (this.valor.length < this.longitudMaxima) this.valor += ' ';
                break;

            default:
                if (this.soloNumeros && !/^\d$/.test(tecla)) return;
                if (this.valor.length >= this.longitudMaxima) return;

                this.valor += this.mayusculas ? tecla.toUpperCase() : tecla;
        }

        this.alCambiar?.(this.valor);
    }

    fijar(valor) {
        this.valor = (valor ?? '').slice(0, this.longitudMaxima);
        this.alCambiar?.(this.valor);
    }

    limpiar() { this.fijar(''); }
}

/**
 * Casillas que muestran el documento caracter por caracter.
 * Se leen mejor a distancia que un input comun.
 */
export class CasillasDocumento {
    constructor(contenedor) {
        this.contenedor = contenedor;
        this.longitud = 8;
        this.dibujar('');
    }

    definirLongitud(longitud) {
        this.longitud = longitud;

        // El CSS necesita la cantidad para calcular el tamano de letra: con
        // 15 casillas la fuente debe ser bastante menor que con 8.
        this.contenedor.style.setProperty('--casillas', longitud);

        this.dibujar('');
    }

    dibujar(valor) {
        this.contenedor.innerHTML = Array.from({ length: this.longitud }, (_, i) =>
            `<div class="casilla ${i < valor.length ? 'llena' : ''}">${valor[i] ?? ''}</div>`
        ).join('');
    }

    marcarError() {
        this.contenedor.classList.add('sacudir');
        setTimeout(() => this.contenedor.classList.remove('sacudir'), 500);
    }
}
