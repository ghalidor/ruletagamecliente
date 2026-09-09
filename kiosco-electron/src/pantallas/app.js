/* ============================================================================
   S3K Kiosco - orquestador.

   Este archivo solo conecta piezas: la logica de dibujo esta en nucleo/rueda,
   la del giro en nucleo/animador, la de estados en nucleo/maquina.
   ============================================================================ */

import { Rueda }                       from '../nucleo/rueda.js';
import { Animador }                    from '../nucleo/animador.js';
import { Api }                         from '../nucleo/api.js';
import { Senal }                       from '../nucleo/senal.js';
import { Maquina, Estado }             from '../nucleo/maquina.js';
import { cargarPiezas, cargarImagenesPremios, Sonido } from '../nucleo/assets.js';
import { Teclado, CasillasDocumento } from './teclado.js';

/**
 * Segundos que la rueda queda desbloqueada sin que nadie la toque.
 * Pasado ese tiempo vuelve a bloquearse: el cliente se fue y no queremos
 * dejarla lista para que un roce dispare un giro.
 */
const SEGUNDOS_BLOQUEO = 15;

/* --------------------------------------------------------------- Estado --- */
const app = {
    config: null,
    api: null,
    senal: null,
    rueda: null,
    animador: null,
    maquina: null,

    ruletaId: null,
    datosRuleta: null,
    jugadaActual: null,
    ruletaPendiente: undefined,   // cambio que llego a mitad de una jugada

    sonidos: {},

    // Registro en dos pasos
    teclado: null,
    casillas: null,
    tipoDocumento: 1,
    campoActivo: 'nombres',
    cliente: {
        nombres: '', apellidoPaterno: '', apellidoMaterno: '',
        correo: '', telefono: '',
        canales: { whatsapp: false, sms: false, llamada: false, email: false }
    }
};

const capas = {};
const $ = id => document.getElementById(id);

/* ------------------------------------------------------------- Arranque --- */
async function iniciar() {
    ['Carga', 'SinRuleta', 'Standby', 'Listo', 'Resultado', 'Documento', 'Datos', 'Error']
        .forEach(nombre => { capas[nombre] = $('capa' + nombre); });

    app.config = await window.kiosco.obtenerConfig();

    if (app.config.ocultarCursor) document.body.classList.add('sin-cursor');
    if (app.config.modoDepuracion) $('diagnostico').hidden = false;

    app.api   = new Api(app.config);
    app.senal = new Senal(app.config);

    const piezas = await cargarPiezas();
    if (piezas.faltantes.length > 0) {
        console.warn('Faltan piezas graficas:', piezas.faltantes.join(', '));
    }

    app.sonidos = {
        giro:   new Sonido('wheel2.wav', { loop: true, volumen: 0.7 }),
        premio: new Sonido('win.mp3',    { volumen: 0.9 }),
        tecla:  new Sonido('beep.mp3',   { volumen: 0.4 })
    };

    app.rueda    = new Rueda($('lienzo'), piezas);
    app.animador = new Animador(app.rueda);
    app.maquina  = new Maquina(alCambiarEstado);

    app.rueda.redimensionar();
    publicarGeometriaRueda();

    prepararEventos();
    prepararAtajosDePrueba();
    ajustarMarco();

    app.senal.alCambiarRuleta     = alRecibirCambioDeRuleta;
    app.senal.alActualizarTajadas   = alRecibirCambioDeTajadas;
    app.senal.alCambiarConfiguracion = alRecibirCambioDeConfiguracion;
    await app.senal.conectar();

    await sincronizarPantalla();

    setInterval(mantenerViva, 30000);
    if (app.config.modoDepuracion) setInterval(pintarDiagnostico, 500);
}

/* ------------------------------------------------------------- Eventos --- */
function prepararEventos() {
    // Pointer events cubre dedo y mouse. El original escuchaba onmousedown
    // sobre TODO el canvas, asi que cualquier roce disparaba un giro.
    // El canvas solo responde con la rueda desbloqueada. En STANDBY los toques
    // los captura la capa de bloqueo.
    $('lienzo').addEventListener('pointerdown', () => {
        if (!app.maquina.es(Estado.LISTO)) return;
        solicitarGiro();
    });

    // Desbloquear
    $('btnJugar').addEventListener('click', () => {
        if (app.maquina.es(Estado.STANDBY)) app.maquina.ir(Estado.LISTO);
    });

    $('btnContinuar').addEventListener('click', volverAStandby);
    $('btnRegistrarme').addEventListener('click', () => app.maquina.ir(Estado.DOCUMENTO));

    $('btnCancelarDocumento').addEventListener('click', () => app.maquina.ir(Estado.RESULTADO));
    $('btnSiguienteDatos').addEventListener('click', pasarADatos);

    $('btnCancelarDatos').addEventListener('click', () => app.maquina.ir(Estado.DOCUMENTO));
    $('btnGuardarCliente').addEventListener('click', guardarCliente);

    $('btnCerrarError').addEventListener('click', volverAStandby);

    // Cada campo del paso 2 es un boton: al tocarlo, el teclado escribe en el.
    $('camposCliente').addEventListener('click', evento => {
        const boton = evento.target.closest('[data-campo]');
        if (boton) enfocarCampo(boton.dataset.campo);
    });

    prepararCanales();

    let temporizadorResize = null;
    window.addEventListener('resize', () => {
        clearTimeout(temporizadorResize);
        temporizadorResize = setTimeout(() => {
            app.rueda.redimensionar();
            publicarGeometriaRueda();
            app.animador.repintar();
            ajustarMarco();
        }, 150);
    });

    // Ni menu contextual ni zoom por pellizco en un totem.
    document.addEventListener('contextmenu', e => e.preventDefault());
    document.addEventListener('gesturestart', e => e.preventDefault());
}

/* ============================================================ Orientacion ===
   El sistema original decidia con window.innerHeight >= 1270, un numero magico
   que fallaba en cualquier pantalla que no fuera la del totem original.

   Ahora se decide por la forma de la pantalla, que es lo que realmente importa:
   en vertical sobra alto para cabecera y pie; en horizontal ese espacio no
   existe y taparian la rueda.
   =========================================================================== */

const PRESETS = {
    vertical:   { ancho: 1080, alto: 1920, nombre: 'Vertical (totem)' },
    horizontal: { ancho: 1920, alto: 1080, nombre: 'Horizontal (TV)' },
    tablet:     { ancho: 1200, alto: 1600, nombre: 'Tablet vertical' }
};

function orientacionActual() {
    return window.innerWidth > window.innerHeight ? 'horizontal' : 'vertical';
}

function ajustarMarco() {
    const orientacion = orientacionActual();

    document.body.dataset.orientacion = orientacion;

    // Cabecera y pie: solo en vertical, y solo si de verdad hay alto de sobra.
    // Con poco alto se comen la rueda, que es lo importante.
    const hayEspacio = orientacion === 'vertical' && window.innerHeight >= 900;

    $('cabecera').hidden = !hayEspacio;
    $('pie').hidden      = !hayEspacio;

    if (app.config?.modoDepuracion) pintarOrientacion(orientacion);
}

/**
 * Publica en CSS donde termina el borde de la rueda.
 *
 * El radio se calcula en JS y depende del viewport, asi que el CSS no tiene
 * forma de saberlo. Con esta variable, el nombre de la ruleta puede quedar
 * pegado al borde inferior sin numeros magicos ni desalineos al redimensionar.
 */
function publicarGeometriaRueda() {
    if (!app.rueda?.radios) return;

    const bordeInferior = app.rueda.alto / 2 + app.rueda.radios.rBordeRuleta;

    document.documentElement.style.setProperty(
        '--rueda-abajo', `${Math.round(bordeInferior)}px`);
}

function pintarOrientacion(orientacion) {
    let aviso = $('avisoOrientacion');

    if (!aviso) {
        aviso = document.createElement('div');
        aviso.id = 'avisoOrientacion';
        aviso.className = 'aviso-orientacion';
        document.body.appendChild(aviso);
    }

    aviso.textContent =
        `${orientacion.toUpperCase()}  ${window.innerWidth}x${window.innerHeight}` +
        `   ·   Ctrl+1 vertical · Ctrl+2 horizontal · Ctrl+3 tablet`;
}

/** Atajos para probar orientaciones. Solo funcionan con npm run dev. */
function prepararAtajosDePrueba() {
    if (!app.config?.modoDepuracion) return;

    const teclas = { Digit1: 'vertical', Digit2: 'horizontal', Digit3: 'tablet' };

    document.addEventListener('keydown', async evento => {
        if (!evento.ctrlKey) return;

        const preset = PRESETS[teclas[evento.code]];
        if (!preset) return;

        evento.preventDefault();

        const resultado = await window.kiosco.cambiarTamano(preset.ancho, preset.alto);

        if (resultado === null) {
            console.warn('Los presets solo funcionan en modo dev (npm run dev).');
            return;
        }

        console.log(
            `${preset.nombre}: ${resultado.ancho}x${resultado.alto}` +
            (resultado.escala < 1 ? ` (reducido al ${Math.round(resultado.escala * 100)}%)` : ''));
    });

    console.log(
        'Presets de prueba: Ctrl+1 vertical, Ctrl+2 horizontal, Ctrl+3 tablet.');
}

/* ------------------------------------------------------ Sincronizacion --- */
async function sincronizarPantalla() {
    const respuesta = await app.api.estadoPantalla();

    if (!respuesta.ok) {
        mostrarError('Sin conexion',
            'No se pudo contactar al servidor. Se reintentara solo.');
        setTimeout(sincronizarPantalla, 10000);
        return;
    }

    await cargarRuleta(respuesta.datos.ruletaAsignadaId);
}

async function cargarRuleta(ruletaId) {
    if (!ruletaId) {
        app.ruletaId = null;
        app.datosRuleta = null;
        app.animador.detener();
        app.maquina.ir(Estado.SIN_RULETA);
        return;
    }

    const respuesta = await app.api.obtenerRuleta(ruletaId);

    if (!respuesta.ok) {
        mostrarError('Ruleta no disponible', respuesta.mensaje);
        return;
    }

    app.ruletaId = ruletaId;
    app.datosRuleta = respuesta.datos;

    const imagenes = await cargarImagenesPremios(
        app.datosRuleta.tajadas, app.config.servidor);

    app.rueda.definirTajadas(app.datosRuleta.tajadas, imagenes);

    $('tituloRuleta').textContent = app.datosRuleta.nombre;

    app.maquina.ir(Estado.STANDBY);
}

/**
 * La tablet cambio la ruleta.
 * Si el pedestal esta jugando, el cambio queda pendiente: no se corta a un
 * cliente en medio de su jugada.
 */
function alRecibirCambioDeRuleta(ruletaId) {
    if (app.maquina.es(Estado.STANDBY, Estado.LISTO, Estado.SIN_RULETA)) {
        cargarRuleta(ruletaId);
    } else {
        app.ruletaPendiente = ruletaId;
        console.log('Cambio de ruleta en espera hasta volver a standby');
    }
}

/**
 * El gestor edito los premios de una ruleta.
 *
 * Solo importa si es la que este pedestal esta mostrando: con varios
 * pedestales, cada uno ignora los avisos de las demas ruletas.
 */
function alRecibirCambioDeTajadas(ruletaId) {
    if (ruletaId !== app.ruletaId) return;

    if (app.maquina.es(Estado.STANDBY, Estado.LISTO)) {
        cargarRuleta(ruletaId);
    } else {
        // Hay alguien jugando: no se le corta la jugada a mitad de camino.
        app.ruletaPendiente = ruletaId;
        console.log('Cambio de premios en espera hasta volver a standby');
    }
}

/**
 * El gestor cambio un ajuste. La configuracion viaja dentro de los datos de la
 * ruleta, asi que se relee esa misma ruleta para traerla actualizada.
 */
function alRecibirCambioDeConfiguracion() {
    if (!app.ruletaId) return;

    if (app.maquina.es(Estado.STANDBY, Estado.LISTO)) {
        cargarRuleta(app.ruletaId);
    } else {
        // Hay alguien jugando: se aplica al volver a espera.
        app.ruletaPendiente = app.ruletaId;
        console.log('Cambio de ajustes en espera hasta volver a standby');
    }
}

function aplicarPendiente() {
    if (app.ruletaPendiente === undefined) return false;

    const id = app.ruletaPendiente;
    app.ruletaPendiente = undefined;
    cargarRuleta(id);
    return true;
}

async function mantenerViva() {
    if (app.senal.conectado) {
        await app.senal.latido();
        return;
    }

    // Sin WebSocket el pedestal quedaria sordo a los cambios de ruleta. Se
    // consulta por HTTP en el mismo latido: no reemplaza al hub, es la red de
    // seguridad para cuando el hub esta caido.
    await app.api.latido();

    if (!app.maquina.es(Estado.STANDBY, Estado.LISTO, Estado.SIN_RULETA)) return;

    const respuesta = await app.api.estadoPantalla();
    if (!respuesta.ok) return;

    const asignada = respuesta.datos.ruletaAsignadaId ?? null;
    if (asignada !== app.ruletaId) {
        console.log('Cambio de ruleta detectado sin hub:', asignada);
        await cargarRuleta(asignada);
    }
}

/* ----------------------------------------------------------------- Giro --- */
async function solicitarGiro() {
    if (!app.maquina.puede(Estado.GIRANDO)) return;

    app.maquina.ir(Estado.GIRANDO);

    // El servidor decide el ganador y devuelve el angulo. El kiosco solo anima.
    const respuesta = await app.api.girar(app.ruletaId);

    if (!respuesta.ok) {
        app.maquina.estado = Estado.GIRANDO;   // para permitir la salida a ERROR
        mostrarError('No se puede jugar ahora', respuesta.mensaje);
        return;
    }

    app.jugadaActual = respuesta.datos;

    app.animador.detenerReposo();
    app.sonidos.giro.reproducir();

    app.animador.girar(
        app.jugadaActual.anguloFinal,
        app.jugadaActual.duracionMs,
        {
            // El volumen sigue a la animacion: baja durante el ultimo tramo y
            // llega a cero justo cuando la rueda para. Antes se desvanecia
            // DESPUES de frenar, y se oia medio segundo de mas.
            alAvanzar: avance => {
                const DESDE = 0.7;   // empieza a bajar al 70% del giro
                if (avance < DESDE) return;

                const tramo = (avance - DESDE) / (1 - DESDE);
                app.sonidos.giro.atenuar(1 - tramo);
            },
            alTerminar: alTerminarGiro
        });
}

function alTerminarGiro(indiceGanador) {
    // Ya venia atenuado a cero por alAvanzar: aca solo se corta.
    app.sonidos.giro.detener();

    // Comprobacion de coherencia: el indice al que llego la rueda debe ser el
    // que el servidor eligio. Si no coincide, hay un desfase de geometria.
    const esperado = app.datosRuleta.tajadas
        .findIndex(t => t.tajadaId === app.jugadaActual.tajadaId);

    if (esperado !== indiceGanador) {
        console.error(
            `Desfase de angulo: la rueda paro en ${indiceGanador} y el servidor eligio ${esperado}`);
    }

    app.maquina.ir(Estado.RESULTADO);
}

/* ============================================================== Registro ====
   Paso 1: tipo y numero de documento.
   Paso 2: nombres, apellidos y contacto, mas el consentimiento.

   Se separa en dos porque el documento define que teclado hace falta (el DNI
   es numerico, el carnet alfanumerico) y porque permite precargar los datos
   de quien ya jugo antes.
   =========================================================================== */

const ETIQUETAS_CAMPO = {
    nombres:         'Nombres',
    apellidoPaterno: 'Apellido paterno',
    apellidoMaterno: 'Apellido materno',
    correo:          'Correo',
    telefono:        'Telefono'
};

/* ------------------------------------------------- Paso 1: documento --- */

function mostrarDocumento() {
    capas.Documento.classList.add('capa-visible');

    // El cliente esta escribiendo: el sonido de premio ya cumplio su funcion.
    app.sonidos.premio.detener();
    clearInterval(app.intervaloCuenta);

    $('errorDocumento').textContent = '';
    app.tipoDocumento = 1;

    pintarTiposDocumento();

    app.casillas = new CasillasDocumento($('casillasDocumento'));

    app.teclado = new Teclado($('tecladoDocumento'), {
        sonido: app.sonidos.tecla,
        alCambiar: valor => {
            app.casillas.dibujar(valor);
            $('btnSiguienteDatos').disabled = !documentoValido(valor);
            reprogramarRegistro();
        }
    });

    aplicarReglasDocumento();
    app.maquina.programarSalida(app.datosRuleta.registroTimeoutSegundos, Estado.STANDBY);
    contarCierre(app.datosRuleta.registroTimeoutSegundos);
}

function pintarTiposDocumento() {
    const tipos = app.datosRuleta.tiposDocumento ?? [
        { valor: 1, nombre: 'DNI', nombreCorto: 'DNI', soloNumeros: true, minimo: 8, maximo: 8 }
    ];

    $('tiposDocumento').innerHTML = tipos.map(t =>
        `<button class="tipo-doc ${t.valor === app.tipoDocumento ? 'activo' : ''}"
                 data-tipo="${t.valor}">${t.nombreCorto || t.nombre}</button>`).join('');

    $('tiposDocumento').onclick = evento => {
        const boton = evento.target.closest('[data-tipo]');
        if (!boton) return;

        app.tipoDocumento = Number(boton.dataset.tipo);

        $('tiposDocumento').querySelectorAll('.tipo-doc')
            .forEach(b => b.classList.toggle('activo', b === boton));

        app.teclado.limpiar();
        aplicarReglasDocumento();
        reprogramarRegistro();
    };
}

function reglaActual() {
    return (app.datosRuleta.tiposDocumento ?? [])
        .find(t => t.valor === app.tipoDocumento)
        ?? { soloNumeros: true, minimo: 8, maximo: 8 };
}

/** El teclado y las casillas se adaptan al tipo elegido. */
function aplicarReglasDocumento() {
    const regla = reglaActual();

    app.casillas.definirLongitud(regla.maximo);

    app.teclado.configurar({
        modo:           regla.soloNumeros ? 'numerico' : 'texto',
        soloNumeros:    regla.soloNumeros,
        longitudMaxima: regla.maximo
    });

    app.teclado.limpiar();
    $('btnSiguienteDatos').disabled = true;
}

function documentoValido(valor) {
    const regla = reglaActual();
    const limpio = (valor ?? '').trim();

    if (limpio.length < regla.minimo || limpio.length > regla.maximo) return false;
    if (regla.soloNumeros && !/^\d+$/.test(limpio)) return false;

    return true;
}

/**
 * Velo de espera. Se usa en las operaciones que consultan al servidor y que
 * el cliente percibe como lentas: buscar el documento puede tardar hasta 4
 * segundos si hay que preguntarle al padron externo.
 */
function mostrarEspera(visible, texto = 'Buscando tus datos...') {
    let velo = $('veloEspera');

    if (!velo) {
        velo = document.createElement('div');
        velo.id = 'veloEspera';
        velo.className = 'velo-espera';
        velo.innerHTML = '<div class="contenido"><div class="aro-carga"></div><p></p></div>';
        document.body.appendChild(velo);
    }

    velo.querySelector('p').textContent = texto;
    velo.classList.toggle('visible', visible);
}

/** Si el documento ya jugo antes, se traen sus datos y no reescribe todo. */
async function pasarADatos() {
    const numero = app.teclado.valor.trim().toUpperCase();

    if (!documentoValido(numero)) {
        $('errorDocumento').textContent = 'Revisa el numero de documento.';
        app.casillas.marcarError();
        return;
    }

    app.numeroDocumento = numero;

    // Doble bloqueo: el velo tapa la pantalla y el boton se deshabilita. Sin
    // esto, cuatro segundos de espera sin senal hacen que el cliente vuelva a
    // tocar y se dispare la consulta dos veces.
    $('btnSiguienteDatos').disabled = true;
    mostrarEspera(true);

    try {
        await buscarYPasar(numero);
    } finally {
        mostrarEspera(false);
        $('btnSiguienteDatos').disabled = false;
    }
}

async function buscarYPasar(numero) {
    const respuesta = await app.api.buscarCliente(app.tipoDocumento, numero);

    if (respuesta.ok && respuesta.datos?.encontrado) {
        const c = respuesta.datos;
        app.cliente = {
            nombres:         c.nombres ?? '',
            apellidoPaterno: c.apellidoPaterno ?? '',
            apellidoMaterno: c.apellidoMaterno ?? '',
            correo:          c.correo ?? '',
            telefono:        c.telefono ?? '',
            canales: {
                whatsapp: c.aceptaWhatsapp === true,
                sms:      c.aceptaSms === true,
                llamada:  c.aceptaLlamada === true,
                email:    c.aceptaEmail === true
            }
        };
        toaster('Ya estabas registrado. Revisa tus datos.', 'ok');
    } else {
        app.cliente = {
            nombres: '', apellidoPaterno: '', apellidoMaterno: '',
            correo: '', telefono: '',
            canales: { whatsapp: false, sms: false, llamada: false, email: false }
        };

        // No estaba en nuestra base: se intenta el padron externo. Solo con
        // DNI, que es el unico tipo que ese servicio conoce.
        if (app.tipoDocumento === 1) {
            mostrarEspera(true, 'Consultando tus datos...');
            await autocompletarDesdePadron(numero);
        }
    }

    app.maquina.ir(Estado.DATOS);
}

/**
 * Rellena nombres y apellidos desde el padron externo.
 *
 * Silencioso ante fallos a proposito: si el servicio esta caido, apagado o
 * lento, el cliente simplemente escribe a mano. Mostrarle un error por algo
 * que no le incumbe seria peor que no ofrecer la comodidad.
 */
async function autocompletarDesdePadron(numeroDocumento) {
    const respuesta = await app.api.consultarPadron(numeroDocumento);

    if (!respuesta.ok || !respuesta.datos?.encontrado) return;

    const p = respuesta.datos;

    app.cliente.nombres         = p.nombres ?? '';
    app.cliente.apellidoPaterno = p.apellidoPaterno ?? '';
    app.cliente.apellidoMaterno = p.apellidoMaterno ?? '';

    toaster('Datos encontrados. Revisalos antes de aceptar.', 'ok');
}

/* ------------------------------------------------ Canales de contacto --- */

const CANALES = ['whatsapp', 'sms', 'llamada', 'email'];

const CASILLA = {
    whatsapp: 'chkWhatsapp',
    sms:      'chkSms',
    llamada:  'chkLlamada',
    email:    'chkEmail',
    ninguno:  'chkNinguno'
};

/**
 * "No autorizo" y los cuatro canales se excluyen entre si.
 *
 * Sin esta regla se puede marcar "no autorizo" y ademas WhatsApp, que es una
 * contradiccion: el cliente estaria diciendo dos cosas opuestas y no habria
 * forma de saber cual vale.
 */
function prepararCanales() {
    CANALES.forEach(canal => {
        $(CASILLA[canal]).addEventListener('change', evento => {
            app.cliente.canales[canal] = evento.target.checked;

            // Elegir un canal desmarca "no autorizo"
            if (evento.target.checked) $(CASILLA.ninguno).checked = false;

            pintarCanales();
            revisarBotonGuardar();
            reprogramarRegistro();
        });
    });

    $(CASILLA.ninguno).addEventListener('change', evento => {
        if (evento.target.checked) {
            // "No autorizo" apaga los cuatro
            CANALES.forEach(canal => {
                app.cliente.canales[canal] = false;
                $(CASILLA[canal]).checked = false;
            });
        }

        pintarCanales();
        revisarBotonGuardar();
        reprogramarRegistro();
    });
}

/** Refleja el estado en las casillas y resalta las elegidas. */
function pintarCanales() {
    CANALES.forEach(canal => {
        const casilla = $(CASILLA[canal]);
        casilla.checked = app.cliente.canales[canal] === true;
        casilla.closest('.notif-opcion').classList.toggle('marcada', casilla.checked);
    });

    const ninguno = $(CASILLA.ninguno);
    ninguno.closest('.notif-opcion').classList.toggle('marcada', ninguno.checked);
}

/** True si el cliente ya respondio: eligio algun canal o marco "no autorizo". */
function respondioNotificaciones() {
    return CANALES.some(c => app.cliente.canales[c]) || $(CASILLA.ninguno).checked;
}

/* ----------------------------------------------------- Paso 2: datos --- */

function mostrarDatos() {
    capas.Datos.classList.add('capa-visible');

    $('errorDatos').textContent = '';
    $('etiquetaDocumento').textContent =
        `${reglaActual().nombre ?? 'Documento'}: ${app.numeroDocumento}`;

    // Marcar como obligatorios los que la configuracion diga
    $('marcaCorreo').textContent   = app.datosRuleta.correoObligatorio   ? '*' : '(opcional)';
    $('marcaTelefono').textContent = app.datosRuleta.telefonoObligatorio ? '*' : '(opcional)';

    // Si ya estaba registrado, se marcan los canales que tenia autorizados.
    $(CASILLA.ninguno).checked =
        !CANALES.some(c => app.cliente.canales[c]) && app.cliente.nombres !== '';

    pintarCanales();

    app.teclado = new Teclado($('tecladoDatos'), {
        modo: 'texto',
        longitudMaxima: 120,
        sonido: app.sonidos.tecla,
        alCambiar: valor => {
            app.cliente[app.campoActivo] = valor;
            pintarCampo(app.campoActivo);
            revisarBotonGuardar();
            reprogramarRegistro();
        }
    });

    enfocarCampo('nombres');
    Object.keys(ETIQUETAS_CAMPO).forEach(pintarCampo);
    revisarBotonGuardar();

    app.maquina.programarSalida(app.datosRuleta.registroTimeoutSegundos, Estado.STANDBY);
    contarCierre(app.datosRuleta.registroTimeoutSegundos);
}

function enfocarCampo(campo) {
    app.campoActivo = campo;

    // Tocar un campo tambien cuenta como actividad. Sin esto, a quien se queda
    // pensando que poner se le cierra el formulario en la cara.
    reprogramarRegistro();

    $('camposCliente').querySelectorAll('[data-campo]')
        .forEach(b => b.classList.toggle('activo', b.dataset.campo === campo));

    $('tituloTeclado').textContent = ETIQUETAS_CAMPO[campo];

    // El telefono es numerico; el resto usa QWERTY.
    app.teclado.configurar({
        modo:           campo === 'telefono' ? 'numerico' : 'texto',
        soloNumeros:    campo === 'telefono',
        longitudMaxima: campo === 'telefono' ? 15 : 120
    });

    app.teclado.fijar(app.cliente[campo] ?? '');
}

function pintarCampo(campo) {
    const elemento = document.querySelector(`[data-valor="${campo}"]`);
    if (elemento) elemento.textContent = app.cliente[campo] ?? '';
}

function revisarBotonGuardar() {
    $('btnGuardarCliente').disabled = !datosCompletos();
}

function datosCompletos() {
    const c = app.cliente;

    if (!c.nombres.trim() || !c.apellidoPaterno.trim()) return false;

    // Hay que responder algo: elegir canales o marcar "no autorizo". Dejar la
    // pregunta en blanco no es una respuesta.
    if (!respondioNotificaciones()) return false;

    if (app.datosRuleta.correoObligatorio   && !c.correo.trim())   return false;
    if (app.datosRuleta.telefonoObligatorio && !c.telefono.trim()) return false;

    return true;
}

async function guardarCliente() {
    if (!datosCompletos()) {
        document.querySelector('.notificaciones')
            ?.classList.toggle('falta', !respondioNotificaciones());
        return;
    }

    $('btnGuardarCliente').disabled = true;
    $('errorDatos').textContent = '';
    mostrarEspera(true, 'Guardando tus datos...');

    const respuesta = await app.api.registrarCliente({
        jugadaId:        app.jugadaActual.jugadaId,
        tipoDocumento:   app.tipoDocumento,
        numeroDocumento: app.numeroDocumento,
        nombres:         app.cliente.nombres.trim(),
        apellidoPaterno: app.cliente.apellidoPaterno.trim(),
        apellidoMaterno: app.cliente.apellidoMaterno.trim() || null,
        correo:          app.cliente.correo.trim() || null,
        telefono:        app.cliente.telefono.trim() || null,

        aceptaWhatsapp:  app.cliente.canales.whatsapp,
        aceptaSms:       app.cliente.canales.sms,
        aceptaLlamada:   app.cliente.canales.llamada,
        aceptaEmail:     app.cliente.canales.email
    });

    mostrarEspera(false);

    if (respuesta.ok) {
        toaster('Tus datos se guardaron correctamente.', 'ok');
        setTimeout(volverAStandby, 1800);
    } else {
        toaster(respuesta.mensaje || 'No se pudieron guardar tus datos.', 'error');
        $('errorDatos').textContent = respuesta.mensaje ?? '';
        $('btnGuardarCliente').disabled = false;
    }
}

/** Segundos restantes a partir de los cuales se avisa que va a cerrarse. */
const AVISO_CIERRE_SEGUNDOS = 15;

/**
 * Cada tecla o toque reinicia la cuenta: quien esta llenando el formulario
 * tranquilo no deberia ver nunca este aviso.
 */
function reprogramarRegistro() {
    const total = app.datosRuleta.registroTimeoutSegundos;

    app.maquina.reprogramar(total, Estado.STANDBY);
    contarCierre(total);
}

function contarCierre(segundos) {
    clearInterval(app.intervaloCierre);

    const aviso = $('avisoTiempo');
    if (!aviso) return;

    aviso.classList.remove('visible');
    let restan = segundos;

    app.intervaloCierre = setInterval(() => {
        restan--;

        if (!app.maquina.es(Estado.DOCUMENTO, Estado.DATOS)) {
            clearInterval(app.intervaloCierre);
            aviso.classList.remove('visible');
            return;
        }

        if (restan <= AVISO_CIERRE_SEGUNDOS && restan > 0) {
            aviso.classList.add('visible');
            aviso.textContent = `Se cerrara en ${restan}s. Toca para continuar.`;
        }

        if (restan <= 0) clearInterval(app.intervaloCierre);
    }, 1000);
}

/* ------------------------------------------------------------- Toaster --- */

/** Aviso flotante. tipo: 'ok' | 'error' */
function toaster(mensaje, tipo = 'ok') {
    const caja = document.createElement('div');
    caja.className = `toaster toaster-${tipo}`;
    caja.innerHTML =
        `<span>${tipo === 'ok' ? '&#10003;' : '&#9888;'}</span><span>${mensaje}</span>`;

    $('zonaToaster').appendChild(caja);

    setTimeout(() => {
        caja.classList.add('saliendo');
        setTimeout(() => caja.remove(), 300);
    }, 3200);
}

function volverAStandby() {
    if (aplicarPendiente()) return;
    app.maquina.ir(app.ruletaId ? Estado.STANDBY : Estado.SIN_RULETA);
}

/* -------------------------------------------------------------- Estados --- */
function alCambiarEstado(nuevo, anterior) {
    console.log(`${anterior} -> ${nuevo}`);

    Object.values(capas).forEach(capa => capa.classList.remove('capa-visible'));

    // El sonido de premio acompana al cartel de ganador y a nada mas. Antes se
    // lanzaba al mostrar el resultado y nadie lo detenia: seguia sonando por
    // debajo del registro y reaparecia desfasado al volver a standby.
    if (anterior === Estado.RESULTADO && nuevo !== Estado.RESULTADO)
        app.sonidos.premio.detener();

    switch (nuevo) {
        case Estado.SIN_RULETA:
            capas.SinRuleta.classList.add('capa-visible');
            app.animador.detener();
            break;

        case Estado.STANDBY:
            capas.Standby.classList.add('capa-visible');
            clearInterval(app.intervaloBloqueo);
            app.jugadaActual = null;

            clearInterval(app.intervaloCierre);
            $('avisoTiempo')?.classList.remove('visible');

            // Red de seguridad: si el timeout cerro el registro mientras una
            // consulta seguia en curso, el velo quedaria tapando la pantalla.
            $('veloEspera')?.classList.remove('visible');

            // Standby es silencio: la rueda esta bloqueada y en espera.
            app.sonidos.premio.detener();
            app.sonidos.giro.detener();
            // Giro lento: un totem quieto no invita a jugar.
            app.animador.iniciarReposo();
            break;

        case Estado.LISTO:
            capas.Listo.classList.add('capa-visible');
            $('tituloRuletaListo').textContent = app.datosRuleta?.nombre ?? '';

            // Si nadie la toca, se vuelve a bloquear sola.
            app.maquina.programarSalida(SEGUNDOS_BLOQUEO, Estado.STANDBY);
            contarBloqueo(SEGUNDOS_BLOQUEO);
            break;

        case Estado.GIRANDO:
            clearInterval(app.intervaloBloqueo);
            break;   // solo la rueda, sin capas encima

        case Estado.RESULTADO:
            mostrarResultado();
            break;

        case Estado.DOCUMENTO:
            mostrarDocumento();
            break;

        case Estado.DATOS:
            mostrarDatos();
            break;

        case Estado.ERROR:
            capas.Error.classList.add('capa-visible');
            break;
    }
}

/** Cuenta regresiva visible antes de que la rueda se vuelva a bloquear. */
function contarBloqueo(segundos) {
    const etiqueta = $('segundosBloqueo');
    let restan = segundos;
    etiqueta.textContent = restan;

    clearInterval(app.intervaloBloqueo);
    app.intervaloBloqueo = setInterval(() => {
        restan--;
        etiqueta.textContent = Math.max(0, restan);

        if (restan <= 0 || !app.maquina.es(Estado.LISTO))
            clearInterval(app.intervaloBloqueo);
    }, 1000);
}

function mostrarResultado() {
    // El registro es opcional a nivel de configuracion: con CLIENTE_PIDE_DATOS
    // en 0 el premio se entrega igual, solo que no se ofrece captar los datos.
    const ofreceRegistro = app.datosRuleta?.pideDatosCliente !== false;

    $('btnRegistrarme').hidden = !ofreceRegistro;

    capas.Resultado.classList.add('capa-visible');
    $('textoPremio').textContent = app.jugadaActual.textoPremio;
    app.sonidos.premio.reproducir();

    // Con un solo boton, que ocupe el ancho: dos botones y uno oculto dejaba
    // el Continuar descentrado.
    $('btnContinuar').closest('.botones')
        ?.classList.toggle('botones-uno', !ofreceRegistro);

    const segundos = app.datosRuleta.standbyTimeoutSegundos;
    app.maquina.programarSalida(segundos, Estado.STANDBY);
    contarAtras(segundos);
}

function contarAtras(segundos) {
    const etiqueta = $('segundosRestantes');
    let restan = segundos;
    etiqueta.textContent = restan;

    clearInterval(app.intervaloCuenta);
    app.intervaloCuenta = setInterval(() => {
        restan--;
        etiqueta.textContent = Math.max(0, restan);

        // Si el cliente entro a registrarse, la cuenta ya no aplica.
        if (restan <= 0 || !app.maquina.es(Estado.RESULTADO)) {
            clearInterval(app.intervaloCuenta);
        }
    }, 1000);
}

function mostrarError(titulo, detalle) {
    $('tituloError').textContent = titulo;
    $('textoError').textContent = detalle;
    app.maquina.ir(Estado.ERROR);
}

/* --------------------------------------------------------- Diagnostico --- */
function pintarDiagnostico() {
    $('diagnostico').textContent = [
        `estado    ${app.maquina.estado}`,
        `ruleta    ${app.ruletaId ?? '-'}`,
        `tajadas   ${app.datosRuleta?.tajadas.length ?? 0}`,
        `signalr   ${app.senal.conectado ? 'conectado' : 'sin conexion'}`,
        `angulo    ${(app.animador.anguloActual * 180 / Math.PI % 360).toFixed(1)}deg`,
        `pendiente ${app.ruletaPendiente ?? '-'}`
    ].join('\n');
}

iniciar().catch(error => {
    console.error('Fallo el arranque:', error);
    document.body.innerHTML =
        `<div style="color:#fff;font:16px sans-serif;padding:40px">
            No se pudo iniciar el kiosco.<br><br>${error.message}
         </div>`;
});