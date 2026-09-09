/* ============================================================================
   Proceso principal de Electron.
   Se encarga de todo lo que el navegador no puede: pantalla completa real,
   bloqueo de atajos, audio sin gesto previo y evitar que el monitor se apague.
   ============================================================================ */

const { app, BrowserWindow, globalShortcut, powerSaveBlocker, ipcMain, screen } = require('electron');
const path = require('path');
const fs = require('fs');

const MODO_DEV = process.argv.includes('--dev');

// --- Audio sin gesto del usuario -------------------------------------------
// En un pedestal desatendido nadie "hace click primero", asi que la politica
// de autoplay de Chromium bloquearia el sonido del giro. Esta es la razon
// principal por la que el kiosco corre en Electron y no en un navegador.
app.commandLine.appendSwitch('autoplay-policy', 'no-user-gesture-required');

let ventana = null;
let bloqueoSuspension = null;

/* ------------------------------------------------------------ Configuracion */
function leerConfig() {
    const candidatas = [
        path.join(process.resourcesPath || '', 'config.json'),
        path.join(__dirname, '..', 'config.json')
    ];

    for (const ruta of candidatas) {
        try {
            if (fs.existsSync(ruta)) return JSON.parse(fs.readFileSync(ruta, 'utf8'));
        } catch (error) {
            console.error('config.json invalido en', ruta, error.message);
        }
    }

    console.error('No se encontro config.json. Se usan valores por defecto.');
    return {
        servidor: 'http://localhost:5000',
        codigoPantalla: 'PEDESTAL-01',
        tokenPantalla: '',
        kiosco: true,
        siempreEncima: true,
        ocultarCursor: true,
        bloquearAtajos: true,
        reinicioDiarioHora: 5
    };
}

const config = leerConfig();

/**
 * Arma la politica de seguridad de contenido con el servidor real.
 *
 * Antes vivia en un <meta> del HTML con localhost fijo, asi que al publicar en
 * otra IP habia que acordarse de cambiarlo en dos lugares. Aca se deriva de
 * config.json: cambias el servidor y la politica se ajusta sola.
 */
function armarCsp() {
    let origen = 'http://localhost:5000';
    let origenWs = 'ws://localhost:5000';

    try {
        const url = new URL(config.servidor);
        origen   = url.origin;
        origenWs = `${url.protocol === 'https:' ? 'wss' : 'ws'}://${url.host}`;
    } catch {
        // config.servidor invalido: se queda el valor por defecto, que es
        // restrictivo. Preferible eso a una politica vacia.
        console.error('config.servidor no es una URL valida:', config.servidor);
    }

    return [
        "default-src 'self' 'unsafe-inline'",
        `connect-src 'self' ${origen} ${origenWs}`,
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
        "font-src 'self' https://fonts.gstatic.com",
        `img-src 'self' file: data: ${origen}`,
        `media-src 'self' file: ${origen}`
    ].join('; ');
}

/* ------------------------------------------------------------------ Ventana */
function crearVentana() {
    ventana = new BrowserWindow({
        show: false,
        backgroundColor: '#000000',
        kiosk: config.kiosco && !MODO_DEV,
        fullscreen: config.kiosco && !MODO_DEV,

        // Opcion propia: no depende de kiosco ni de dev, para poder probar el
        // comportamiento del pedestal con la ventana normal.
        alwaysOnTop: config.siempreEncima === true,
        frame: MODO_DEV,
        autoHideMenuBar: true,
        width: 1080,
        height: 1920,          // el pedestal es vertical
        webPreferences: {
            preload: path.join(__dirname, 'puente.js'),
            contextIsolation: true,
            nodeIntegration: false,
            sandbox: false
        }
    });

    // La politica se inyecta por cabecera antes de cargar la pagina.
    const csp = armarCsp();

    ventana.webContents.session.webRequest.onHeadersReceived((detalles, responder) => {
        responder({
            responseHeaders: {
                ...detalles.responseHeaders,
                'Content-Security-Policy': [csp]
            }
        });
    });

    ventana.loadFile(path.join(__dirname, 'pantallas', 'index.html'));

    ventana.once('ready-to-show', () => {
        ventana.show();
        // 'screen-saver' es el nivel mas alto: queda por encima incluso de la
        // barra de tareas. Con alwaysOnTop a secas, Windows lo deja debajo.
        if (config.siempreEncima === true)
            ventana.setAlwaysOnTop(true, 'screen-saver');

        if (MODO_DEV) ventana.webContents.openDevTools({ mode: 'detach' });
    });

    // Watchdog: si el render muere, se recarga solo en vez de dejar
    // el pedestal con una pantalla en blanco hasta que alguien lo note.
    ventana.webContents.on('render-process-gone', (_evento, detalle) => {
        console.error('El render murio:', detalle.reason);
        setTimeout(() => ventana && !ventana.isDestroyed() && ventana.reload(), 2000);
    });

    ventana.webContents.on('did-fail-load', (_e, codigo, descripcion) => {
        console.error('Fallo la carga:', codigo, descripcion);
        setTimeout(() => ventana && !ventana.isDestroyed() && ventana.reload(), 3000);
    });

    ventana.on('closed', () => { ventana = null; });
}

/* --------------------------------------------------------------- Blindaje */
function bloquearAtajos() {
    if (!config.bloquearAtajos || MODO_DEV) return;

    // Sin esto, un cliente curioso cierra la app o recarga la pagina.
    const bloqueados = [
        'CommandOrControl+W', 'CommandOrControl+R', 'CommandOrControl+Shift+R',
        'CommandOrControl+Q', 'Alt+F4', 'F11', 'F5',
        'CommandOrControl+Shift+I', 'CommandOrControl+M', 'CommandOrControl+N'
    ];

    bloqueados.forEach(atajo => {
        try { globalShortcut.register(atajo, () => {}); }
        catch { /* algunos no se pueden capturar segun el sistema */ }
    });

    // Salida de emergencia para el tecnico.
    globalShortcut.register('CommandOrControl+Alt+Shift+Q', () => {
        console.log('Salida de emergencia');
        app.exit(0);
    });
}

function evitarSuspension() {
    // Un totem no debe apagar la pantalla ni entrar en suspension.
    bloqueoSuspension = powerSaveBlocker.start('prevent-display-sleep');
}

/* -------------------------------------------------------- Reinicio diario */
function programarReinicio() {
    const hora = config.reinicioDiarioHora;
    if (typeof hora !== 'number' || hora < 0 || hora > 23) return;

    // Doce horas de canvas acumulan memoria. Un reinicio de madrugada, cuando
    // la sala esta cerrada, evita que el rendimiento se degrade sin que nadie
    // sepa por que.
    setInterval(() => {
        const ahora = new Date();
        if (ahora.getHours() === hora && ahora.getMinutes() === 0) {
            console.log('Reinicio diario programado');
            app.relaunch();
            app.exit(0);
        }
    }, 60000);
}

/* --------------------------------------------------------------- Arranque */
app.whenReady().then(() => {
    crearVentana();
    bloquearAtajos();
    evitarSuspension();
    programarReinicio();
});

app.on('window-all-closed', () => app.quit());

app.on('will-quit', () => {
    globalShortcut.unregisterAll();
    if (bloqueoSuspension !== null) powerSaveBlocker.stop(bloqueoSuspension);
});

/* ------------------------------------------------ Canal con la interfaz */
ipcMain.handle('obtener-config', () => ({
    servidor: config.servidor,
    codigoPantalla: config.codigoPantalla,
    tokenPantalla: config.tokenPantalla,
    ocultarCursor: config.ocultarCursor,
    modoDepuracion: config.modoDepuracion === true || MODO_DEV
}));

ipcMain.handle('salir', () => app.exit(0));

/**
 * Cambia el tamano de la ventana para probar orientaciones.
 *
 * Solo en modo dev: en el pedestal la ventana es fija y del tamano de la
 * pantalla. Sirve para ver como se acomoda la interfaz sin tener que girar
 * un monitor de verdad.
 *
 * Si el preset no entra en la pantalla, se reduce proporcionalmente.
 */
ipcMain.handle('cambiar-tamano', (_evento, ancho, alto) => {
    if (!MODO_DEV || !ventana || ventana.isDestroyed()) return null;

    const { workAreaSize } = screen.getPrimaryDisplay();

    const escala = Math.min(
        1,
        (workAreaSize.width  - 80) / ancho,
        (workAreaSize.height - 80) / alto);

    const anchoFinal = Math.round(ancho * escala);
    const altoFinal  = Math.round(alto  * escala);

    ventana.setSize(anchoFinal, altoFinal);
    ventana.center();

    return { ancho: anchoFinal, alto: altoFinal, escala };
});
