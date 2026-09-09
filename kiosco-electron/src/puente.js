/* ============================================================================
   Puente entre el proceso principal y la interfaz.
   Expone lo minimo indispensable: con contextIsolation activo, la pagina no
   tiene acceso a Node, y asi debe quedarse.
   ============================================================================ */

const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('kiosco', {
    obtenerConfig: () => ipcRenderer.invoke('obtener-config'),
    salir:         () => ipcRenderer.invoke('salir'),

    /** Solo tiene efecto en modo dev. Para probar orientaciones. */
    cambiarTamano: (ancho, alto) => ipcRenderer.invoke('cambiar-tamano', ancho, alto)
});