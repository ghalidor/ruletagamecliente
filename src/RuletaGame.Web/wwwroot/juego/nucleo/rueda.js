/* ============================================================================
   Dibujo de la rueda.

   Diferencia clave con el sistema original: alli se creaba un canvas por cada
   indice posible (N canvases identicos de ~1000x1000 en memoria) y se
   redibujaban todas las tajadas en cada frame, lo que obligaba a bajar a 15
   fps con un limitador manual.

   Aca las capas estaticas se dibujan UNA vez en buffers offscreen y por frame
   solo se rota el buffer de tajadas. Eso es lo que permite 60 fps reales.
   ============================================================================ */

import { calcularRadios, radioBase, sectorDibujo, indiceEnPuntero } from './geometria.js';

/** Paleta de la rueda fisica. El color sale del orden de la tajada. */
const COLORES = ['#27CC0A', '#D6A902', '#D40108', '#9604A6', '#0261DC'];

const FUENTE = 'Comic Sans MS';
const LUCES_BORDE = 24;

export class Rueda {
    constructor(canvas, piezas) {
        this.canvas  = canvas;
        this.ctx     = canvas.getContext('2d', { alpha: true });
        this.piezas  = piezas;

        this.tajadas  = [];
        this.imagenes = {};
        this.angulo   = 0;

        this.bufferMarco   = null;   // aro exterior + fondo de la rueda
        this.bufferTajadas = null;   // las porciones de color: esto es lo que rota
        this.bufferAdornos = null;   // tachones negros y flecha
        this.faseLuces     = 0;      // desplazamiento de las luces animadas
    }

    /** Carga los datos y fuerza el redibujo de los buffers. */
    definirTajadas(tajadas, imagenes = {}) {
        this.tajadas  = tajadas;
        this.imagenes = imagenes;
        this.invalidar();
    }

    invalidar() {
        this.bufferMarco = this.bufferTajadas = this.bufferAdornos = null;
    }

    /** Ajusta el canvas al viewport y al DPI real de la pantalla. */
    redimensionar() {
        // Sin esto la rueda se ve borrosa en un monitor 4K, que es lo que
        // suele tener un totem moderno.
        const dpr = Math.min(window.devicePixelRatio || 1, 2);

        this.ancho = window.innerWidth;
        this.alto  = window.innerHeight;

        this.canvas.width  = this.ancho * dpr;
        this.canvas.height = this.alto  * dpr;
        this.canvas.style.width  = this.ancho + 'px';
        this.canvas.style.height = this.alto  + 'px';

        this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

        this.r      = radioBase(this.ancho, this.alto);
        this.radios = calcularRadios(this.r);

        this.invalidar();
    }

    /* ------------------------------------------------------------ Buffers */

    nuevoBuffer() {
        const lado = 10 + 2 * this.r * 1.25 | 0;
        const buffer = document.createElement('canvas');
        buffer.width = buffer.height = lado;
        return buffer;
    }

    construirMarco() {
        const buffer = this.nuevoBuffer();
        const ctx = buffer.getContext('2d');
        const cx = buffer.width / 2, cy = buffer.height / 2;
        const { rBordeRuleta, rRuletaImagen } = this.radios;

        this.dibujarCentrada(ctx, this.piezas.borderuleta, cx, cy, rBordeRuleta);
        this.dibujarCentrada(ctx, this.piezas.ruleta,      cx, cy, rRuletaImagen);

        this.bufferMarco = buffer;
    }

    /** Las porciones de color con su texto e imagen. Se dibuja una sola vez. */
    construirTajadas() {
        const total = this.tajadas.length;
        const lado  = 2 * this.r + 10;

        const buffer = document.createElement('canvas');
        buffer.width = buffer.height = lado;

        const ctx = buffer.getContext('2d');
        const cx = 5 + this.r, cy = 5 + this.r;

        for (let i = 0; i < total; i++) {
            const { desde, hasta } = sectorDibujo(i, total);

            ctx.beginPath();
            ctx.moveTo(cx, cy);
            ctx.arc(cx, cy, this.r, desde, hasta, false);
            ctx.lineTo(cx, cy);

            ctx.lineWidth = 2;
            ctx.strokeStyle = '#ffffff';
            ctx.stroke();

            ctx.fillStyle = COLORES[i % COLORES.length];
            ctx.fill();

            ctx.shadowBlur = 10;
            ctx.shadowColor = 'black';
            ctx.fill();
            ctx.shadowBlur = 0;

            this.dibujarContenidoTajada(ctx, cx, cy, i, total);
        }

        this.bufferTajadas = buffer;
    }

    dibujarContenidoTajada(ctx, cx, cy, indice, total) {
        const tajada = this.tajadas[indice];
        const { desde } = sectorDibujo(indice, total);
        const anguloMedio = desde + (Math.PI / total);

        ctx.save();
        ctx.translate(cx, cy);
        ctx.rotate(anguloMedio);

        this.dibujarTexto(ctx, tajada);
        this.dibujarImagenPremio(ctx, tajada);

        ctx.restore();
    }

    dibujarTexto(ctx, tajada) {
        const { rBordeSpin, rTajadaImagen } = this.radios;

        // Los premios en dinero disponen de mas ancho porque el texto es corto.
        const anchoDisponible = tajada.tipo === 0
            ? rTajadaImagen - rBordeSpin * 1.2
            : rTajadaImagen - rBordeSpin * 1.55;

        const lineas = this.partirTexto(tajada.texto);
        const masLarga = lineas.reduce((a, b) => a.length >= b.length ? a : b, '');

        ctx.fillStyle = 'white';
        ctx.textAlign = 'left';
        ctx.textBaseline = 'middle';
        ctx.shadowColor = '#000';
        ctx.shadowBlur = this.r / 100;

        const tamano = this.ajustarFuente(ctx, masLarga, anchoDisponible);
        const x = rBordeSpin * 1.3;

        if (lineas.length > 1) {
            ctx.fillText(lineas[0], x, -tamano * 0.55);
            ctx.fillText(lineas[1], x,  tamano * 0.55);
        } else {
            ctx.fillText(lineas[0], x, 0);
        }

        ctx.shadowBlur = 0;
    }

    /**
     * Busca el tamano de fuente que entra en el ancho disponible.
     * El original hacia un bucle decreciente desde 300px probando cada valor;
     * esto lo resuelve con una sola medicion y una regla de tres.
     */
    ajustarFuente(ctx, texto, anchoDisponible) {
        const REFERENCIA = 100;
        ctx.font = `bold ${REFERENCIA}px "${FUENTE}"`;

        const anchoMedido = ctx.measureText(texto).width || 1;
        const tamano = Math.max(8, Math.floor(REFERENCIA * anchoDisponible / anchoMedido));

        ctx.font = `bold ${tamano}px "${FUENTE}"`;
        return tamano;
    }

    /** Parte en dos lineas si el texto es largo y tiene espacios. */
    partirTexto(texto) {
        if (texto.length <= 10 || !texto.includes(' ')) return [texto];

        const palabras = texto.split(' ');
        const mitad = Math.ceil(palabras.length / 2);

        return [
            palabras.slice(0, mitad).join(' '),
            palabras.slice(mitad).join(' ')
        ];
    }

    dibujarImagenPremio(ctx, tajada) {
        const imagen = this.imagenes[tajada.tajadaId];
        if (!imagen) return;

        const { rTajadaImagen, rBordeSpin } = this.radios;
        const radio = (rTajadaImagen - rBordeSpin) * 0.35 / 2;
        const x = rTajadaImagen;

        ctx.save();
        ctx.beginPath();
        ctx.arc(x, 0, radio, 0, 2 * Math.PI);
        ctx.clip();
        ctx.drawImage(imagen, x - radio, -radio, radio * 2, radio * 2);
        ctx.restore();
    }

    /** Tachones negros entre tajadas y flecha del puntero. */
    construirAdornos() {
        const buffer = this.nuevoBuffer();
        const ctx = buffer.getContext('2d');
        const cx = buffer.width / 2, cy = buffer.height / 2;

        const total = this.tajadas.length;
        const { rPuntosNegros, rCirculoFlecha } = this.radios;
        const radioAnillo = this.r * 0.999;

        for (let i = 1; i <= total; i++) {
            const angulo = (Math.PI * 2 / total) * i;
            const x = radioAnillo * Math.cos(angulo) + cx;
            const y = radioAnillo * Math.sin(angulo) + cy;

            this.dibujarCentrada(ctx, this.piezas.puntoSombra, x, y, rPuntosNegros * 1.7);
            this.dibujarCentrada(ctx, this.piezas.puntoNegro,  x, y, rPuntosNegros);
        }

        // Flecha roja, arriba y fija.
        if (this.piezas.flecha) {
            ctx.save();
            ctx.translate(cx, cy);
            ctx.drawImage(
                this.piezas.flecha,
                4 - rCirculoFlecha,
                -this.r * 1.15 - rCirculoFlecha,
                rCirculoFlecha * 1.8,
                rCirculoFlecha * 3.1);
            ctx.restore();
        }

        this.bufferAdornos = buffer;
    }

    /* ------------------------------------------------------------- Dibujo */

    /**
     * Pinta un frame.
     * @param {number} angulo rotacion en radianes
     * @param {boolean} animarLuces true en standby y durante el giro
     */
    dibujar(angulo, animarLuces = false) {
        if (this.tajadas.length === 0) return;

        if (!this.bufferMarco)   this.construirMarco();
        if (!this.bufferTajadas) this.construirTajadas();
        if (!this.bufferAdornos) this.construirAdornos();

        this.angulo = angulo;

        const ctx = this.ctx;
        const cx = this.ancho / 2, cy = this.alto / 2;

        ctx.clearRect(0, 0, this.ancho, this.alto);

        ctx.drawImage(this.bufferMarco,
            cx - this.bufferMarco.width / 2, cy - this.bufferMarco.height / 2);

        // Lo unico que rota es este buffer ya dibujado.
        ctx.save();
        ctx.translate(cx, cy);
        ctx.rotate(angulo);
        ctx.drawImage(this.bufferTajadas,
            -this.bufferTajadas.width / 2, -this.bufferTajadas.height / 2);
        ctx.restore();

        this.dibujarCentrada(ctx, this.piezas.sombratajadas, cx, cy, this.radios.rSombraTajadas);

        ctx.drawImage(this.bufferAdornos,
            cx - this.bufferAdornos.width / 2, cy - this.bufferAdornos.height / 2);

        this.dibujarLucesBorde(ctx, cx, cy, animarLuces);
        this.dibujarCentro(ctx, cx, cy, animarLuces);
    }

    /**
     * Las 24 lamparas del aro.
     * En el original eran estaticas; aca la fase avanza y las luces corren,
     * que es lo que hace que una ruleta parezca viva.
     */
    dibujarLucesBorde(ctx, cx, cy, animar) {
        const { rBordeSpin } = this.radios;
        const radioAnillo = this.r * 1.190;

        if (animar) this.faseLuces += 0.05;

        for (let i = 1; i <= LUCES_BORDE; i++) {
            const angulo = (Math.PI * 2 / LUCES_BORDE) * i;
            const x = radioAnillo * Math.cos(angulo) + cx;
            const y = radioAnillo * Math.sin(angulo) + cy;

            const paso = animar ? Math.floor(this.faseLuces) : 0;
            const { imagen, radio } = this.elegirLuz(i + paso, rBordeSpin, 'borde');

            this.dibujarCentrada(ctx, imagen, x, y, radio);
        }
    }

    dibujarCentro(ctx, cx, cy, animar) {
        const { rSombraSpin, rBordeSpin, rSpin, rPuntosSpin } = this.radios;

        this.dibujarCentrada(ctx, this.piezas.puntoSombra, cx, cy, rSombraSpin);
        this.dibujarCentrada(ctx, this.piezas.spinborde,   cx, cy, rBordeSpin);
        this.dibujarCentrada(ctx, this.piezas.spin,        cx, cy, rSpin);

        const total = this.tajadas.length;
        const radioAnillo = rBordeSpin * 0.94;

        for (let i = 1; i <= total; i++) {
            const angulo = (Math.PI * 2 / total) * i;
            const x = radioAnillo * Math.cos(angulo) + cx;
            const y = radioAnillo * Math.sin(angulo) + cy;

            const paso = animar ? Math.floor(this.faseLuces) : 0;
            const { imagen, radio } = this.elegirLuz(i + paso, rBordeSpin, 'spin');

            this.dibujarCentrada(ctx, imagen, x, y, radio);
        }
    }

    /** Alterna las tres piezas de luz. Cada una tiene su tamano. */
    elegirLuz(indice, rBordeSpin, zona) {
        const cual = ((indice % 3) + 3) % 3;

        const tamanos = zona === 'borde'
            ? [rBordeSpin / 10, rBordeSpin / 7, rBordeSpin / 3]
            : [rBordeSpin / 12, rBordeSpin / 9, rBordeSpin / 5];

        const piezas = [this.piezas.luz1, this.piezas.luz2, this.piezas.luz3];

        return { imagen: piezas[cual], radio: tamanos[cual] };
    }

    /** Dibuja una pieza centrada en (x, y) con el radio dado. */
    dibujarCentrada(ctx, imagen, x, y, radio) {
        if (!imagen) return;
        ctx.drawImage(imagen, x - radio, y - radio, radio * 2, radio * 2);
    }

    /** Indice de la tajada que quedo bajo el puntero. */
    indiceGanador() {
        return indiceEnPuntero(this.angulo, this.tajadas.length);
    }

    /** True si el punto tocado cae dentro del boton central. */
    tocoElCentro(x, y) {
        const dx = x - this.ancho / 2;
        const dy = y - this.alto / 2;
        return Math.sqrt(dx * dx + dy * dy) <= this.radios.rBordeSpin;
    }
}
