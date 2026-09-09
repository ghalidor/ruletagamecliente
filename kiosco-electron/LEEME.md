# S3K Kiosco — pedestal

App Electron para el pedestal táctil. Consume el servicio ASP.NET Core.

## Antes de arrancar

1. **Imágenes** → `src/assets/imagenes/` (11 obligatorias, ver el `LEEME.txt` de ahí)
2. **Audios** → `src/assets/audio/` (3 archivos)
3. **SignalR** → `src/vendor/signalr.min.js` (ver `LEEME.txt`)
4. **Configuración** → editar `config.json`:
   - `servidor` — dónde escucha el servicio
   - `codigoPantalla` — debe coincidir con el registrado en el gestor
   - `tokenPantalla` — el mismo de `Kiosco:TokenPantalla` en `appsettings.json`

## Correr

```
npm install
npm run dev      # ventana normal, con DevTools
npm start        # modo kiosco real
npm run build    # instalador .exe en build/salida
```

## Estructura

```
src/
├── principal.js          proceso Electron: kiosco, atajos, watchdog
├── puente.js             contextBridge (la página no toca Node)
├── nucleo/
│   ├── geometria.js      radios y ángulos
│   ├── easing.js         curvas de aceleración
│   ├── assets.js         carga de imágenes y audio
│   ├── rueda.js          dibujo en canvas
│   ├── animador.js       bucle de animación
│   ├── api.js            cliente HTTP
│   ├── senal.js          SignalR
│   └── maquina.js        máquina de estados
├── pantallas/
│   ├── index.html
│   ├── kiosco.css
│   ├── teclado.js        teclado numérico táctil
│   └── app.js            orquestador
└── assets/
```

Ningún archivo pasa de 350 líneas. La lógica de dibujo, la de animación y la de
estados están separadas: se puede tocar una sin romper las otras.

## Diferencias con el sistema original

| Original | Ahora |
|---|---|
| 15 fps con limitador manual sobre `Date.now()` | 60 fps con el timestamp de `requestAnimationFrame` |
| Redibujaba todas las tajadas en cada frame | Buffers offscreen; por frame solo rota uno |
| N canvases idénticos de ~1000×1000 en memoria | Uno |
| Sin `devicePixelRatio` (borroso en 4K) | Escalado por DPR |
| Duración = valor × 4 ms, sin avisar | Milisegundos reales |
| Luces del borde estáticas | Animadas, corren por el aro |
| El ángulo global no se reseteaba (salto en el 2º giro) | Continúa desde donde quedó |
| Loop de audio con corte en 28.7s (clic audible) | Loop nativo + desvanecido |
| `win.mp3` con `play()` y `pause()` seguidos | Suena |
| Sin modo de espera | Giro lento + luces + "TOCA PARA JUGAR" |
| `onmousedown` sobre todo el canvas | Pointer events, con zona definida |
| Bucle de fuente decreciente desde 300px | Una medición y regla de tres |
| El ganador se sorteaba en el navegador | Lo decide el servidor; el kiosco solo anima |
| Modal abierto para siempre | Timeout configurable, que se pausa al escribir el DNI |
| Sin teclado (el DNI lo tecleaba el operador) | Teclado numérico táctil |

## Salida de emergencia

`Ctrl + Alt + Shift + Q` cierra la app en modo kiosco.
