/* ============================================================================
   S3K Kiosco - utilidades comunes del gestor
   Un solo archivo, sin modulos ni build.
   ============================================================================ */

const S3K = {

    /* ------------------------------------------------------------- Peticiones */

    /** Token antiforgery que Razor Pages exige en todo POST. */
    get token() {
        return document.querySelector('meta[name="antiforgery"]')?.content || '';
    },

    /** POST JSON. Devuelve { ok, datos, mensaje } y nunca lanza. */
    async post(url, cuerpo) {
        try {
            const respuesta = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    // Sin esta cabecera el servidor responde 400 y nada se guarda.
                    'RequestVerificationToken': this.token
                },
                body: JSON.stringify(cuerpo || {})
            });

            if (respuesta.status === 400)
                console.warn('POST rechazado (revisa el token antiforgery):', url);

            const texto = await respuesta.text();
            const datos = texto ? JSON.parse(texto) : null;

            return {
                ok: respuesta.ok,
                datos,
                mensaje: datos?.mensaje || (respuesta.ok ? '' : 'No se pudo completar la operacion.')
            };
        } catch (error) {
            console.error(url, error);
            return { ok: false, datos: null, mensaje: 'Sin conexion con el servidor.' };
        }
    },

    async get(url) {
        try {
            const respuesta = await fetch(url);
            if (!respuesta.ok) return { ok: false, datos: null, mensaje: 'No se pudo leer los datos.' };
            return { ok: true, datos: await respuesta.json(), mensaje: '' };
        } catch (error) {
            console.error(url, error);
            return { ok: false, datos: null, mensaje: 'Sin conexion con el servidor.' };
        }
    },

    /* ---------------------------------------------------------------- Avisos */

    /** Aviso flotante. tipo: 'ok' | 'error' | 'info' */
    aviso(mensaje, tipo = 'ok') {
        let zona = document.getElementById('zonaAvisos');
        if (!zona) {
            zona = document.createElement('div');
            zona.id = 'zonaAvisos';
            zona.style.cssText =
                'position:fixed;top:18px;right:18px;z-index:9999;display:flex;' +
                'flex-direction:column;gap:8px;max-width:360px';
            document.body.appendChild(zona);
        }

        const icono = tipo === 'ok' ? 'check-circle-fill'
            : tipo === 'error' ? 'exclamation-triangle-fill'
                : 'info-circle-fill';

        const caja = document.createElement('div');
        caja.className = `aviso aviso-${tipo}`;
        caja.style.cssText = 'margin:0;box-shadow:0 10px 30px rgba(0,0,0,.5);background:var(--superficie)';
        caja.innerHTML = `<i class="bi bi-${icono}"></i><span>${mensaje}</span>`;
        zona.appendChild(caja);

        setTimeout(() => {
            caja.style.transition = 'opacity .3s, transform .3s';
            caja.style.opacity = '0';
            caja.style.transform = 'translateX(20px)';
            setTimeout(() => caja.remove(), 300);
        }, 4000);
    },

    /** Confirmacion con el mismo lenguaje visual que el resto del gestor. */
    confirmar(titulo, detalle, textoAccion = 'Confirmar') {
        return new Promise(resolver => {
            const id = 'modalConfirmar';
            document.getElementById(id)?.remove();

            document.body.insertAdjacentHTML('beforeend', `
            <div class="modal fade" id="${id}" tabindex="-1">
              <div class="modal-dialog modal-dialog-centered">
                <div class="modal-content" style="background:var(--superficie);
                     border:1px solid var(--borde);border-radius:var(--radio);
                     box-shadow:var(--sombra-3)">
                  <div class="modal-body" style="padding:26px">
                    <h3 style="margin-bottom:8px">${titulo}</h3>
                    <p style="color:var(--texto-medio);margin:0 0 22px">${detalle}</p>
                    <div style="display:flex;gap:10px;justify-content:flex-end">
                      <button class="btn" data-bs-dismiss="modal">Cancelar</button>
                      <button class="btn btn-peligro" id="${id}Aceptar">${textoAccion}</button>
                    </div>
                  </div>
                </div>
              </div>
            </div>`);

            const elemento = document.getElementById(id);
            const modal = new bootstrap.Modal(elemento);
            let aceptado = false;

            document.getElementById(`${id}Aceptar`).onclick = () => { aceptado = true; modal.hide(); };
            elemento.addEventListener('hidden.bs.modal', () => {
                elemento.remove();
                resolver(aceptado);
            });

            modal.show();
        });
    },

    /* ------------------------------------------------------------ Componentes */

    /** DataTable con el idioma y los defaults del proyecto. */
    tabla(selector, opciones = {}) {
        return $(selector).DataTable(Object.assign({
            pageLength: 15,
            lengthChange: false,
            autoWidth: false,

            // Buscador arriba, informacion y paginado abajo. El layout por
            // defecto de DataTables mete todo junto y se ve apretado.
            dom: '<"tabla-superior"f>rt<"tabla-inferior"ip>',
            language: {
                search: '',
                searchPlaceholder: 'Buscar...',
                info: '_START_ a _END_ de _TOTAL_',
                infoEmpty: 'Sin registros',
                infoFiltered: '(de _MAX_)',
                zeroRecords: 'No hay coincidencias',
                emptyTable: 'Sin registros',
                paginate: { first: 'Primero', last: 'Ultimo', next: '&rsaquo;', previous: '&lsaquo;' },
                aria: { sortAscending: ': ordenar ascendente', sortDescending: ': ordenar descendente' }
            }
        }, opciones));
    },

    /** Select2 con buscador. */
    select(selector, opciones = {}) {
        return $(selector).select2(Object.assign({
            width: '100%',
            language: {
                noResults: () => 'Sin resultados',
                searching: () => 'Buscando...'
            }
        }, opciones));
    },

    /** Selector de fecha en espanol, formato dd/mm/aaaa. */
    fecha(selector, opciones = {}) {
        return flatpickr(selector, Object.assign({
            locale: 'es',
            dateFormat: 'd/m/Y',
            allowInput: true
        }, opciones));
    },

    moneda(valor, simbolo = 'S/.') {
        return simbolo + Number(valor || 0).toLocaleString('es-PE', {
            minimumFractionDigits: 2, maximumFractionDigits: 2
        });
    },

    /** Cuenta ascendente. Respeta prefers-reduced-motion. */
    contar(elemento, hasta, formato = v => Math.round(v)) {
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            elemento.textContent = formato(hasta);
            return;
        }

        const duracion = 700;
        const inicio = performance.now();

        const paso = ahora => {
            const avance = Math.min((ahora - inicio) / duracion, 1);
            elemento.textContent = formato(hasta * (1 - Math.pow(1 - avance, 3)));
            if (avance < 1) requestAnimationFrame(paso);
        };

        requestAnimationFrame(paso);
    }
};

/* --------------------------------------------------------------- Arranque */
document.addEventListener('DOMContentLoaded', () => {

    // Select2, DataTables y flatpickr traen su CSS claro por defecto.
    const estilo = document.createElement('style');
    estilo.textContent = `
        .select2-container--default .select2-selection--single {
            background: var(--superficie-2); border: 1px solid var(--borde-fuerte);
            border-radius: var(--radio-sm); height: 44px;
        }
        .select2-container--default .select2-selection--single .select2-selection__rendered {
            color: var(--texto); line-height: 42px; padding-left: 13px;
        }
        .select2-container--default .select2-selection--single .select2-selection__arrow { height: 42px; }
        .select2-dropdown {
            background: var(--superficie); border: 1px solid var(--borde);
            box-shadow: var(--sombra-3); border-radius: var(--radio-sm);
        }
        .select2-container--default .select2-results__option { color: var(--texto-medio); padding: 10px 13px; }
        .select2-container--default .select2-results__option--highlighted[aria-selected] {
            background: var(--marca-suave); color: var(--marca-fuerte);
        }
        .select2-search--dropdown .select2-search__field {
            background: var(--superficie-2); border: 1px solid var(--borde-fuerte);
            color: var(--texto); border-radius: var(--radio-sm); padding: 8px 11px;
        }
        .flatpickr-calendar {
            background: var(--superficie); border: 1px solid var(--borde);
            box-shadow: var(--sombra-3);
        }
        .flatpickr-day { color: var(--texto-medio); }
        .flatpickr-day:hover { background: var(--superficie-2); }
        .flatpickr-day.selected { background: var(--marca); border-color: var(--marca); color: var(--marca-texto); }
        .flatpickr-months .flatpickr-month, .flatpickr-weekday,
        .flatpickr-current-month input.cur-year, .numInputWrapper span {
            color: var(--texto) !important; fill: var(--texto) !important;
        }
        .flatpickr-months .flatpickr-prev-month svg, .flatpickr-months .flatpickr-next-month svg {
            fill: var(--texto-medio);
        }
        /* Los estilos de la tabla viven en gestor.css: tenerlos aca los hacia
           ganar por orden de insercion y era imposible ajustarlos. */
        .modal-content { background: var(--superficie) !important; color: var(--texto); }
        .modal-backdrop.show { opacity: .5; }
    `;
    document.head.appendChild(estilo);

    /* ------------------------------------------------------------- Tema */
    const raiz = document.documentElement;

    function pintarTema(tema) {
        raiz.setAttribute('data-tema', tema);
        localStorage.setItem('tema', tema);

        const esOscuro = tema === 'oscuro';
        const icono = document.getElementById('iconoTema');
        const texto = document.getElementById('textoTema');

        if (icono) icono.className = esOscuro ? 'bi bi-sun' : 'bi bi-moon-stars';
        if (texto) texto.textContent = esOscuro ? 'Tema claro' : 'Tema oscuro';
    }

    pintarTema(localStorage.getItem('tema') || 'claro');

    document.getElementById('btnTema')?.addEventListener('click', evento => {
        evento.preventDefault();
        pintarTema(raiz.getAttribute('data-tema') === 'oscuro' ? 'claro' : 'oscuro');
    });

    /* ---------------------------------------------- Menu colapsable */
    // El estado se recuerda: si el operador contrajo el menu, sigue contraido
    // al navegar entre paginas.
    if (localStorage.getItem('barraMin') === '1') document.body.classList.add('barra-min');
    raiz.classList.remove('pre-barra-min');

    document.getElementById('btnMenu')?.addEventListener('click', () => {
        const minimizado = document.body.classList.toggle('barra-min');
        localStorage.setItem('barraMin', minimizado ? '1' : '0');
    });

    /* ----------------------------------------------------------- Salir */
    document.getElementById('btnSalir')?.addEventListener('click', async evento => {
        evento.preventDefault();
        await S3K.post('/api/auth/logout');
        location.href = '/Login';
    });
});