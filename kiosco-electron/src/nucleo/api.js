/* ============================================================================
   Comunicacion con el servicio.
   El pedestal se autentica con un token fijo de pantalla: aca no hay un
   usuario que inicie sesion.
   ============================================================================ */

export class Api {
    constructor({ servidor, tokenPantalla, codigoPantalla }) {
        this.servidor = servidor.replace(/\/$/, '');
        this.token    = tokenPantalla;
        this.codigo   = codigoPantalla;
    }

    get cabeceras() {
        return {
            'Content-Type': 'application/json',
            'X-Pantalla-Token': this.token
        };
    }

    async pedir(ruta, opciones = {}) {
        try {
            const respuesta = await fetch(this.servidor + ruta, {
                headers: this.cabeceras,
                ...opciones
            });

            const texto = await respuesta.text();
            const datos = texto ? JSON.parse(texto) : null;

            return {
                ok: respuesta.ok,
                datos,
                mensaje: datos?.mensaje || (respuesta.ok ? '' : 'Error del servidor.')
            };
        } catch (error) {
            console.error(ruta, error);
            return { ok: false, datos: null, mensaje: 'Sin conexion con el servidor.' };
        }
    }

    /** Datos de la pantalla, incluida la ruleta que tiene asignada. */
    estadoPantalla() {
        return this.pedir(`/api/kiosco/pantalla/${encodeURIComponent(this.codigo)}`);
    }

    /** Tajadas y parametros de la ruleta. Nunca trae las probabilidades. */
    obtenerRuleta(ruletaId) {
        return this.pedir(`/api/kiosco/ruleta/${ruletaId}`);
    }

    /** El servidor sortea y devuelve el angulo. El kiosco solo anima. */
    girar(ruletaId) {
        return this.pedir('/api/kiosco/girar', {
            method: 'POST',
            body: JSON.stringify({ ruletaId, codigoPantalla: this.codigo })
        });
    }

    /** Registra al cliente que gano, con todos sus datos. */
    registrarCliente(datos) {
        return this.pedir('/api/kiosco/registrar', {
            method: 'POST',
            body: JSON.stringify(datos)
        });
    }

    /** Busca si el documento ya esta registrado, para precargar los datos. */
    /**
     * Consulta los nombres en el padron externo. Solo tiene sentido con DNI.
     * Nunca devuelve error: si el servicio esta caido o deshabilitado,
     * responde { encontrado: false } y el cliente escribe a mano.
     */
    consultarPadron(numeroDocumento) {
        return this.pedir(`/api/kiosco/padron/${encodeURIComponent(numeroDocumento)}`);
    }

    buscarCliente(tipoDocumento, numeroDocumento) {
        return this.pedir(
            `/api/kiosco/cliente/${tipoDocumento}/${encodeURIComponent(numeroDocumento)}`);
    }

    latido() {
        return this.pedir('/api/kiosco/latido', {
            method: 'POST',
            body: JSON.stringify({ codigoPantalla: this.codigo })
        });
    }
}
