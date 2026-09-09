/* ============================================================================
   Vista previa de la rueda.

   Dibuja la misma geometria que usa el pedestal, pero con formas propias en vez
   de las piezas PNG. Asi el operador ve como queda la rueda sin necesidad de
   tener los assets del kiosco instalados en el servidor.

   No es un simulador del juego: no gira ni sortea. Solo muestra la
   distribucion, los colores y los textos.
   ============================================================================ */

const PreviaRueda = {

    /** Misma paleta que el pedestal. El color sale del orden de la tajada. */
    COLORES: ['#27CC0A', '#D6A902', '#D40108', '#9604A6', '#0261DC'],

    /**
     * @param {HTMLCanvasElement} lienzo
     * @param {Array} tajadas  [{ texto, color, activo }]
     */
    dibujar(lienzo, tajadas) {
        const activas = tajadas.filter(t => t.activo !== false);
        const ctx = lienzo.getContext('2d');

        // Nitidez en pantallas de alta densidad, incluidas las tablets.
        const dpr  = Math.min(window.devicePixelRatio || 1, 2);
        const lado = lienzo.clientWidth || 320;

        lienzo.width = lienzo.height = lado * dpr;
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, lado, lado);

        if (activas.length === 0) {
            this.dibujarVacio(ctx, lado);
            return;
        }

        const cx = lado / 2, cy = lado / 2;
        const r  = lado / 2 - 14;

        this.dibujarTajadas(ctx, cx, cy, r, activas);
        this.dibujarAro(ctx, cx, cy, r);
        this.dibujarCentro(ctx, cx, cy, r);
        this.dibujarPuntero(ctx, cx, cy, r);
    },

    dibujarTajadas(ctx, cx, cy, r, tajadas) {
        const total = tajadas.length;

        tajadas.forEach((tajada, i) => {
            // El puntero esta arriba, de ahi el desfase de -PI/2.
            const desde = (Math.PI * 2 * i / total) - Math.PI / 2;
            const hasta = desde + (Math.PI * 2 / total);

            ctx.beginPath();
            ctx.moveTo(cx, cy);
            ctx.arc(cx, cy, r, desde, hasta);
            ctx.closePath();

            ctx.fillStyle = tajada.color || this.COLORES[i % this.COLORES.length];
            ctx.fill();

            ctx.strokeStyle = 'rgba(255,255,255,.85)';
            ctx.lineWidth = 2;
            ctx.stroke();

            this.dibujarTexto(ctx, cx, cy, r, tajada.texto, desde, total);
        });
    },

    dibujarTexto(ctx, cx, cy, r, texto, anguloDesde, total) {
        if (!texto) return;

        const medio = anguloDesde + Math.PI / total;

        ctx.save();
        ctx.translate(cx, cy);
        ctx.rotate(medio);

        ctx.fillStyle = '#fff';
        ctx.textAlign = 'right';
        ctx.textBaseline = 'middle';
        ctx.shadowColor = 'rgba(0,0,0,.45)';
        ctx.shadowBlur = 3;

        // Con muchas tajadas el arco se angosta: la letra baja de tamano.
        const tamano = Math.max(9, Math.min(17, 150 / total + 6));
        ctx.font = `600 ${tamano}px Outfit, sans-serif`;

        const recortado = texto.length > 14 ? texto.slice(0, 13) + '…' : texto;
        ctx.fillText(recortado, r - 12, 0);

        ctx.restore();
    },

    /** Lee un color del tema activo, para que la previa no desentone. */
    variable(nombre, respaldo) {
        const valor = getComputedStyle(document.documentElement)
            .getPropertyValue(nombre).trim();
        return valor || respaldo;
    },

    dibujarAro(ctx, cx, cy, r) {
        ctx.beginPath();
        ctx.arc(cx, cy, r + 6, 0, Math.PI * 2);
        ctx.strokeStyle = this.variable('--texto', '#1F2937');
        ctx.lineWidth = 11;
        ctx.stroke();

        // Las 16 lamparas del borde, en version simplificada.
        for (let i = 0; i < 16; i++) {
            const angulo = (Math.PI * 2 / 16) * i;
            const x = cx + (r + 6) * Math.cos(angulo);
            const y = cy + (r + 6) * Math.sin(angulo);

            ctx.beginPath();
            ctx.arc(x, y, 3, 0, Math.PI * 2);
            ctx.fillStyle = i % 2 ? '#FDE68A' : '#F0C419';
            ctx.fill();
        }
    },

    dibujarCentro(ctx, cx, cy, r) {
        const radio = r * 0.26;

        ctx.beginPath();
        ctx.arc(cx, cy, radio, 0, Math.PI * 2);
        ctx.fillStyle = this.variable('--texto', '#1F2937');
        ctx.fill();

        ctx.beginPath();
        ctx.arc(cx, cy, radio - 5, 0, Math.PI * 2);
        ctx.fillStyle = this.variable('--oro', '#F0C419');
        ctx.fill();

        ctx.fillStyle = this.variable('--fondo', '#1F2937');
        ctx.font = `700 ${Math.max(9, radio * 0.42)}px Outfit, sans-serif`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText('GIRA', cx, cy);
    },

    dibujarPuntero(ctx, cx, cy, r) {
        const y = cy - r - 12;

        ctx.beginPath();
        ctx.moveTo(cx, y + 20);
        ctx.lineTo(cx - 10, y);
        ctx.lineTo(cx + 10, y);
        ctx.closePath();

        ctx.fillStyle = '#DC2626';
        ctx.fill();
        ctx.strokeStyle = this.variable('--superficie', '#fff');
        ctx.lineWidth = 2;
        ctx.stroke();
    },

    dibujarVacio(ctx, lado) {
        const cx = lado / 2, cy = lado / 2, r = lado / 2 - 14;

        ctx.beginPath();
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        ctx.fillStyle = this.variable('--superficie-2', '#F4F6FB');
        ctx.fill();
        ctx.strokeStyle = this.variable('--borde-fuerte', '#D2D9E6');
        ctx.lineWidth = 2;
        ctx.setLineDash([7, 6]);
        ctx.stroke();
        ctx.setLineDash([]);

        ctx.fillStyle = this.variable('--texto-tenue', '#9AA3B2');
        ctx.font = '500 14px Outfit, sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText('Sin premios activos', cx, cy);
    },

    /** Redibuja al cambiar el tamano. Devuelve la funcion para poder soltarla. */
    observar(lienzo, obtenerTajadas) {
        const redibujar = () => this.dibujar(lienzo, obtenerTajadas());

        redibujar();
        window.addEventListener('resize', redibujar);

        return redibujar;
    }
};
