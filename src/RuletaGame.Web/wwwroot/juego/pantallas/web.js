/* ============================================================================
   Terminal web - orquestador.

   Es el mismo juego del pedestal. Las diferencias son tres y todas viven en
   este archivo:

   1. Acceso con clave en vez de token fijo del archivo de configuracion.
   2. Sin SignalR: la rueda se recarga al desbloquear, que en una terminal que
      se abre y cierra funciona mejor que mantener un WebSocket vivo.
   3. Version de rueda: si cambian los premios mientras alguien juega, el
      servidor rechaza el giro y la terminal recarga y reintenta sola.

   Este archivo solo conecta piezas: la logica de dibujo esta en nucleo/rueda,
   la del giro en nucleo/animador, la de estados en nucleo/maquina.
   ============================================================================ */

import { Rueda } from '/juego/nucleo/rueda.js';
import { Animador } from '/juego/nucleo/animador.js';
import { Api } from '/juego/nucleo/api.js';
import { Maquina, Estado } from '/juego/nucleo/maquina.js';
import { cargarPiezas, cargarImagenesPremios, Sonido } from '/juego/nucleo/assets.js';
import { Teclado, CasillasDocumento } from '/juego/pantallas/teclado.js';

/**
 * Segundos que la rueda queda desbloqueada sin que nadie la toque.
 * Pasado ese tiempo vuelve a bloquearse: el cliente se fue y no queremos
 * dejarla lista para que un roce dispare un giro.
 */
const SEGUNDOS_BLOQUEO = 15;

/* --------------------------------------------------------------- Estado --- */
const app = {
    api: null,
    terminal: null,
    rueda: null,
    animador: null,
    maquina: null,

    ruletaId: null,
    datosRuleta: null,
    versionRueda: null,
    jugadaActual: null,

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

    app.api = new Api();

    const piezas = await cargarPiezas();
    if (piezas.faltantes.length > 0)
        console.warn('Faltan piezas graficas:', piezas.faltantes.join(', '));

    app.sonidos = {
        giro: new Sonido('wheel2.wav', { loop: true, volumen: 0.7 }),
        premio: new Sonido('win.mp3', { volumen: 0.9 }),
        tecla: new Sonido('beep.mp3', { volumen: 0.4 })
    };

    app.rueda = new Rueda($('lienzo'), piezas);
    app.animador = new Animador(app.rueda);
    app.maquina = new Maquina(alCambiarEstado);

    app.rueda.redimensionar();
    publicarGeometriaRueda();

    prepararEventos();
    prepararAcceso();
    ajustarMarco();

    // Si la cookie sigue viva, entra directo sin pedir la clave.
    const estado = await app.api.estado();

    if (estado.ok) {
        await entrarALaTerminal(estado.datos);
    } else {
        capas.Carga.classList.remove('capa-visible');
        $('capaAcceso').classList.add('capa-visible');
        $('campoClave').focus();
    }

    setInterval(mantenerViva, 60000);
}

/* -------------------------------------------------------------- Acceso --- */

function prepararAcceso() {
    const campo = $('campoClave');

    $('btnAcceder').addEventListener('click', acceder);
    campo.addEventListener('keydown', e => { if (e.key === 'Enter') acceder(); });
    campo.addEventListener('input', () => {
        campo.classList.remove('invalido');
        $('errorAcceso').textContent = '';
    });

    $('btnBloquear').addEventListener('click', bloquearTerminal);
}

async function acceder() {
    const campo = $('campoClave');
    const clave = campo.value.trim();

    if (!clave) { campo.classList.add('invalido'); return; }

    $('btnAcceder').disabled = true;
    $('errorAcceso').textContent = '';

    const respuesta = await app.api.acceder(clave);

    $('btnAcceder').disabled = false;

    if (!respuesta.ok) {
        campo.classList.add('invalido');
        $('errorAcceso').textContent = respuesta.mensaje;
        campo.select();
        return;
    }

    campo.value = '';
    await entrarALaTerminal(respuesta.datos);
}

async function entrarALaTerminal(datos) {
    app.terminal = datos;

    $('capaAcceso').classList.remove('capa-visible');
    $('btnBloquear').classList.add('visible');

    ocultarCargas();

    // El primer gesto del operador habilita el audio del navegador y permite
    // pedir pantalla completa: son cosas que el navegador solo concede tras
    // una interaccion. En Electron esto no hacia falta.
    pedirPantallaCompleta();
    evitarApagadoDePantalla();

    await cargarRuleta(datos.ruletaAsignadaId ?? null);
}

/**
 * Dialogo de confirmacion con el lenguaje visual del juego.
 *
 * No se usa confirm() porque lo dibuja el navegador y rompe la estetica en
 * plena pantalla de juego. Tampoco una libreria externa: el pedestal debe
 * funcionar sin internet, y esto son treinta lineas.
 */
function confirmar(titulo, detalle, textoAceptar = 'Aceptar') {
    return new Promise(resolver => {
        document.getElementById('dialogoConfirmar')?.remove();

        document.body.insertAdjacentHTML('beforeend', `
            <div class="capa capa-modal capa-visible capa-dialogo" id="dialogoConfirmar">
                <div class="tarjeta-dialogo">
                    <div class="dialogo-icono">&#9888;</div>
                    <h2>${titulo}</h2>
                    <p class="tenue">${detalle}</p>
                    <div class="botones">
                        <button class="boton boton-oro" id="dlgAceptar">${textoAceptar}</button>
                        <button class="boton" id="dlgCancelar">Cancelar</button>
                    </div>
                </div>
            </div>`);

        const capa = document.getElementById('dialogoConfirmar');

        const cerrar = respuesta => { capa.remove(); resolver(respuesta); };

        document.getElementById('dlgAceptar').onclick = () => cerrar(true);
        document.getElementById('dlgCancelar').onclick = () => cerrar(false);

        // Tocar fuera de la tarjeta equivale a cancelar.
        capa.onclick = evento => { if (evento.target === capa) cerrar(false); };
    });
}

async function bloquearTerminal() {
    const confirmado = await confirmar(
        'Bloquear la terminal',
        'Habra que escribir la clave de nuevo para volver a jugar.',
        'Bloquear');

    if (!confirmado) return;

    await app.api.salir();

    app.animador.detener();
    app.terminal = null;

    $('btnBloquear').classList.remove('visible');
    $('capaAcceso').classList.add('capa-visible');
    $('campoClave').focus();
}

/** Pantalla completa. Si el navegador la niega, el juego funciona igual. */
async function pedirPantallaCompleta() {
    try {
        if (!document.fullscreenElement)
            await document.documentElement.requestFullscreen({ navigationUI: 'hide' });
    } catch { /* en escritorio no siempre se concede y no es critico */ }
}

/** Mantiene la pantalla encendida. En una tablet sin esto se apaga sola. */
async function evitarApagadoDePantalla() {
    try {
        if ('wakeLock' in navigator) app.wakeLock = await navigator.wakeLock.request('screen');
    } catch { /* no todos los navegadores lo soportan */ }
}

// Al volver de segundo plano hay que volver a pedirlo: el navegador lo suelta.
document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible' && app.terminal) evitarApagadoDePantalla();
});

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

    // Desbloquear. A diferencia del pedestal, aca se consulta al servidor
    // antes de habilitar: es lo que reemplaza a SignalR. La terminal se abre
    // y cierra, y mantener un WebSocket vivo en un navegador da mas problemas
    // que releer la rueda una vez por jugada.
    $('btnJugar').addEventListener('click', desbloquear);

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


/**
 * Publica en CSS donde termina el borde de la rueda.
 *
 * El radio se calcula en JS y depende del viewport, asi que el CSS no tiene
 * forma de saberlo. Con esta variable el nombre de la ruleta queda pegado al
 * borde inferior sin numeros magicos ni desalineos al redimensionar.
 */
function publicarGeometriaRueda() {
    if (!app.rueda?.radios) return;

    const bordeInferior = app.rueda.alto / 2 + app.rueda.radios.rBordeRuleta;

    document.documentElement.style.setProperty(
        '--rueda-abajo', `${Math.round(bordeInferior)}px`);
}

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
    $('pie').hidden = !hayEspacio;

}

/* ------------------------------------------------------ Sincronizacion --- */
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

    // Huella de la configuracion. Viaja en cada giro para que el servidor
    // detecte si esta terminal quedo con una rueda vieja.
    app.versionRueda = respuesta.datos.versionRueda ?? null;

    const imagenes = await cargarImagenesPremios(app.datosRuleta.tajadas);

    app.rueda.definirTajadas(app.datosRuleta.tajadas, imagenes);

    $('tituloRuleta').textContent = app.datosRuleta.nombre;

    // La rueda ya esta lista: no debe quedar ningun indicador de carga.
    ocultarCargas();

    app.maquina.ir(Estado.STANDBY);
}

/**
 * Latido: mantiene vivo el indicador "en linea" del gestor y detecta que la
 * sesion vencio antes de que el cliente toque nada.
 */
async function mantenerViva() {
    if (!app.terminal) return;

    const estado = await app.api.estado();

    if (estado.estado === 401) await bloquearTerminal();
}

/* ------------------------------------------------------------ Desbloqueo --- */

/**
 * Antes de habilitar la rueda se relee del servidor: asi el cliente siempre
 * juega con la configuracion vigente, aunque el gestor la haya cambiado
 * mientras la terminal estaba en espera.
 */
async function desbloquear() {
    if (!app.maquina.es(Estado.STANDBY)) return;

    try {
        await desbloquearInterno();
    } finally {
        // El velo se oculta pase lo que pase. Si una excepcion lo dejara
        // visible, la terminal quedaria tapada y sin forma de seguir.
        mostrarCarga(false);
    }
}

async function desbloquearInterno() {
    mostrarCarga(true);

    // Tiempo minimo visible: en localhost la respuesta llega en 20ms y el
    // loader parpadea sin que nadie lo vea. El operador necesita percibir que
    // la rueda se releyo, no solo que ocurrio.
    const arranque = performance.now();

    const estado = await app.api.estado();

    if (!estado.ok) {
        await esperarMinimo(arranque);

        // 401: la sesion vencio. Se vuelve a pedir la clave.
        if (estado.estado === 401) { await bloquearTerminal(); return; }

        mostrarError('Sin conexion', 'No se pudo contactar al servidor.');
        return;
    }

    const asignada = estado.datos.ruletaAsignadaId ?? null;

    // Se recarga siempre, no solo cuando cambio la ruleta: los premios de la
    // misma ruleta tambien pueden haber cambiado.
    await cargarRuleta(asignada);
    await esperarMinimo(arranque);

    if (app.maquina.es(Estado.STANDBY)) app.maquina.ir(Estado.LISTO);
}

/** Completa hasta MINIMO_CARGA_MS desde el instante dado. */
function esperarMinimo(arranque, minimo = MINIMO_CARGA_MS) {
    const falta = minimo - (performance.now() - arranque);
    return falta > 0 ? new Promise(r => setTimeout(r, falta)) : Promise.resolve();
}

/** Milisegundos que el loader se muestra como minimo. */
const MINIMO_CARGA_MS = 450;

/**
 * Oculta TODO indicador de carga: el velo del desbloqueo y la capa inicial.
 *
 * Se hace explicito y no por estado porque un loader colgado tapa la terminal
 * sin dejar salida, y eso en un pedestal es peor que cualquier otro fallo.
 */
function ocultarCargas() {
    document.getElementById('veloCarga')?.classList.remove('visible');
    document.getElementById('capaCarga')?.classList.remove('capa-visible');
}

function mostrarCarga(visible) {
    let velo = $('veloCarga');

    if (!velo) {
        velo = document.createElement('div');
        velo.id = 'veloCarga';
        velo.className = 'velo-carga';
        velo.innerHTML =
            '<div class="carga-contenido">' +
            '<div class="aro-carga"></div>' +
            '<p>Actualizando la ruleta...</p>' +
            '</div>';
        document.body.appendChild(velo);
    }

    velo.classList.toggle('visible', visible);

    // Al ocultar el velo tambien se cierra la capa inicial, por si quedo
    // abierta: son dos indicadores distintos y basta que uno se cuelgue.
    if (!visible) document.getElementById('capaCarga')?.classList.remove('capa-visible');
}

/* ----------------------------------------------------------------- Giro --- */
async function solicitarGiro(esReintento = false) {
    if (!app.maquina.puede(Estado.GIRANDO)) return;

    app.maquina.ir(Estado.GIRANDO);

    const respuesta = await app.api.girar(app.ruletaId, app.versionRueda);

    // 409: el gestor cambio los premios mientras esta terminal jugaba. El
    // servidor se niega a sortear con datos viejos, porque el angulo se
    // calcula sobre la cantidad de tajadas y apuntaria a otro sector.
    //
    // Se recarga y se reintenta UNA vez. El cliente solo nota medio segundo
    // mas de espera; nunca ve un premio que no esta en pantalla.
    if (respuesta.estado === 409 && !esReintento) {
        console.log('La rueda cambio: recargando y reintentando');

        app.maquina.estado = Estado.LISTO;   // permitir el reintento

        mostrarCarga(true);
        try {
            await cargarRuleta(app.ruletaId);
        } finally {
            mostrarCarga(false);
        }

        return solicitarGiro(true);
    }

    if (!respuesta.ok) {
        app.maquina.estado = Estado.GIRANDO;   // para permitir la salida a ERROR

        if (respuesta.estado === 401) { await bloquearTerminal(); return; }

        mostrarError('No se puede jugar ahora',
            respuesta.estado === 409
                ? 'La ruleta cambio. Vuelve a intentarlo.'
                : respuesta.mensaje);
        return;
    }

    app.jugadaActual = respuesta.datos;

    // El servidor devuelve la version vigente: la terminal se sincroniza sola.
    if (app.jugadaActual.versionRueda) app.versionRueda = app.jugadaActual.versionRueda;

    app.animador.detenerReposo();
    app.sonidos.giro.reproducir();

    app.animador.girar(
        app.jugadaActual.anguloFinal,
        app.jugadaActual.duracionMs,
        {
            alAvanzar: avance => {
                const DESDE = 0.7;
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
    nombres: 'Nombres',
    apellidoPaterno: 'Apellido paterno',
    apellidoMaterno: 'Apellido materno',
    correo: 'Correo',
    telefono: 'Telefono'
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
        modo: regla.soloNumeros ? 'numerico' : 'texto',
        soloNumeros: regla.soloNumeros,
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

/** Si el documento ya jugo antes, se traen sus datos y no reescribe todo. */
async function pasarADatos() {
    const numero = app.teclado.valor.trim().toUpperCase();

    if (!documentoValido(numero)) {
        $('errorDocumento').textContent = 'Revisa el numero de documento.';
        app.casillas.marcarError();
        return;
    }

    app.numeroDocumento = numero;

    const respuesta = await app.api.buscarCliente(app.tipoDocumento, numero);

    if (respuesta.ok && respuesta.datos?.encontrado) {
        const c = respuesta.datos;
        app.cliente = {
            nombres: c.nombres ?? '',
            apellidoPaterno: c.apellidoPaterno ?? '',
            apellidoMaterno: c.apellidoMaterno ?? '',
            correo: c.correo ?? '',
            telefono: c.telefono ?? '',
            canales: {
                whatsapp: c.aceptaWhatsapp === true,
                sms: c.aceptaSms === true,
                llamada: c.aceptaLlamada === true,
                email: c.aceptaEmail === true
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
        if (app.tipoDocumento === 1) await autocompletarDesdePadron(numero);
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

    app.cliente.nombres = p.nombres ?? '';
    app.cliente.apellidoPaterno = p.apellidoPaterno ?? '';
    app.cliente.apellidoMaterno = p.apellidoMaterno ?? '';

    toaster('Datos encontrados. Revisalos antes de aceptar.', 'ok');
}

/* ------------------------------------------------ Canales de contacto --- */

const CANALES = ['whatsapp', 'sms', 'llamada', 'email'];

const CASILLA = {
    whatsapp: 'chkWhatsapp',
    sms: 'chkSms',
    llamada: 'chkLlamada',
    email: 'chkEmail',
    ninguno: 'chkNinguno'
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
    $('marcaCorreo').textContent = app.datosRuleta.correoObligatorio ? '*' : '(opcional)';
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
        modo: campo === 'telefono' ? 'numerico' : 'texto',
        soloNumeros: campo === 'telefono',
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

    if (app.datosRuleta.correoObligatorio && !c.correo.trim()) return false;
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

    const respuesta = await app.api.registrarCliente({
        jugadaId: app.jugadaActual.jugadaId,
        tipoDocumento: app.tipoDocumento,
        numeroDocumento: app.numeroDocumento,
        nombres: app.cliente.nombres.trim(),
        apellidoPaterno: app.cliente.apellidoPaterno.trim(),
        apellidoMaterno: app.cliente.apellidoMaterno.trim() || null,
        correo: app.cliente.correo.trim() || null,
        telefono: app.cliente.telefono.trim() || null,

        aceptaWhatsapp: app.cliente.canales.whatsapp,
        aceptaSms: app.cliente.canales.sms,
        aceptaLlamada: app.cliente.canales.llamada,
        aceptaEmail: app.cliente.canales.email
    });

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
    // Sin cambios pendientes: la rueda se relee al desbloquear, asi que basta
    // con volver a espera.
    ocultarCargas();
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
iniciar().catch(error => {
    console.error('Fallo el arranque:', error);
    document.body.innerHTML =
        `<div style="color:#fff;font:16px sans-serif;padding:40px">
            No se pudo iniciar el kiosco.<br><br>${error.message}
         </div>`;
});