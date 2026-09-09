/* ============================================================================
   Comunicacion de la terminal web con el servidor.

   Se autentica con la cookie que deja /api/web/acceder, no con el token fijo
   del pedestal: aca hay una persona que escribe una clave.
   ============================================================================ */

export class Api {
    async pedir(ruta, opciones = {}) {
        try {
            const respuesta = await fetch(ruta, {
                headers: { 'Content-Type': 'application/json' },
                credentials: 'same-origin',
                ...opciones
            });

            const texto = await respuesta.text();
            const datos = texto ? JSON.parse(texto) : null;

            return {
                ok: respuesta.ok,
                estado: respuesta.status,
                datos,
                mensaje: datos?.mensaje || (respuesta.ok ? '' : 'Error del servidor.')
            };
        } catch (error) {
            console.error(ruta, error);
            return { ok: false, estado: 0, datos: null, mensaje: 'Sin conexion con el servidor.' };
        }
    }

    acceder(clave) {
        return this.pedir('/api/web/acceder', {
            method: 'POST',
            body: JSON.stringify({ clave })
        });
    }

    salir() { return this.pedir('/api/web/salir', { method: 'POST' }); }

    estado() { return this.pedir('/api/web/estado'); }

    obtenerRuleta(ruletaId) { return this.pedir(`/api/web/ruleta/${ruletaId}`); }

    /**
     * El servidor sortea y devuelve el angulo.
     * Si responde 409, la rueda dibujada quedo vieja: hay que recargarla y
     * reintentar. Ver TerminalService.CalcularVersionAsync en el backend.
     */
    girar(ruletaId, versionRueda) {
        return this.pedir('/api/web/girar', {
            method: 'POST',
            body: JSON.stringify({ ruletaId, versionRueda })
        });
    }

    registrarCliente(datos) {
        return this.pedir('/api/web/registrar', {
            method: 'POST',
            body: JSON.stringify(datos)
        });
    }

    /**
     * Consulta los nombres en el padron externo. Solo tiene sentido con DNI.
     * Nunca devuelve error: si el servicio esta caido o deshabilitado,
     * responde { encontrado: false } y el cliente escribe a mano.
     */
    consultarPadron(numeroDocumento) {
        return this.pedir(`/api/web/padron/${encodeURIComponent(numeroDocumento)}`);
    }

    buscarCliente(tipoDocumento, numeroDocumento) {
        return this.pedir(
            `/api/web/cliente/${tipoDocumento}/${encodeURIComponent(numeroDocumento)}`);
    }
}
