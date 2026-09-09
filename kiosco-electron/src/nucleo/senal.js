/* ============================================================================
   Canal en tiempo real con el servidor (SignalR).
   La tablet cambia la ruleta del pedestal y el aviso llega por aca.
   ============================================================================ */

export class Senal {
    constructor({ servidor, codigoPantalla }) {
        this.servidor = servidor.replace(/\/$/, '');
        this.codigo   = codigoPantalla;
        this.conexion = null;

        // Enganches que define app.js
        this.alCambiarRuleta     = null;
        this.alActualizarTajadas = null;
    }

    async conectar() {
        if (typeof signalR === 'undefined') {
            console.warn('SignalR no cargo. El kiosco seguira sin avisos en vivo.');
            return false;
        }

        this.conexion = new signalR.HubConnectionBuilder()
            .withUrl(`${this.servidor}/hub/kiosco`)
            // Sin reconexion automatica, un reinicio del servicio deja al
            // pedestal sordo para siempre.
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .build();

        this.conexion.on('RuletaAsignada', ruletaId => {
            console.log('La tablet asigno la ruleta', ruletaId);
            this.alCambiarRuleta?.(ruletaId);
        });

        // El gestor cambio los premios: activo, elimino, edito o toco un
        // porcentaje. Hay que releer las tajadas y redibujar la rueda.
        this.conexion.on('TajadasActualizadas', ruletaId => {
            console.log('El gestor cambio los premios de la ruleta', ruletaId);
            this.alActualizarTajadas?.(ruletaId);
        });

        // Al reconectar hay que resincronizar: el cambio pudo ocurrir mientras
        // estuvo caido y ese aviso ya se perdio.
        this.conexion.onreconnected(() => {
            console.log('Reconectado. Resincronizando.');
            this.registrar();
        });

        this.conexion.onreconnecting(error =>
            console.warn('Conexion perdida, reintentando:', error?.message ?? ''));

        this.conexion.onclose(error =>
            console.error('Hub cerrado definitivamente:', error?.message ?? ''));

        try {
            await this.conexion.start();
            console.log('Hub conectado a', `${this.servidor}/hub/kiosco`);
            await this.registrar();
            return true;
        } catch (error) {
            console.error(
                'No se pudo conectar al hub:', error.message,
                '\nSi el mensaje menciona CORS o "Failed to fetch", revisa que el',
                'backend tenga la politica CORS "Kiosco" activa en Program.cs.');
            return false;
        }
    }

    async registrar() {
        try {
            await this.conexion?.invoke('Registrar', this.codigo);
        } catch (error) {
            console.error('Fallo el registro en el hub:', error.message);
        }
    }

    async latido() {
        try {
            await this.conexion?.invoke('Latido', this.codigo);
        } catch { /* el latido no es critico */ }
    }

    get conectado() {
        return this.conexion?.state === 'Connected';
    }
}