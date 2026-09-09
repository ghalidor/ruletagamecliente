# RuletaGame

Reemplazo del sistema ASP.NET MVC 5 (`S3K_NEWRULETA`). Tres piezas:

| Pieza | Qué es |
|---|---|
| **Base de datos** | `database/` — 3 scripts SQL Server |
| **Backend + Gestor Ruleta** | `src/` — ASP.NET Core 8 como servicio de Windows |
| **Kiosco** | `kiosco-electron/` — app Electron para el pedestal táctil |

Todo corre en la misma PC del pedestal.

---

## Instalación

### 1. Base de datos
```
sqlcmd -S localhost -i database\01_esquema.sql
sqlcmd -S localhost -i database\02_datos_iniciales.sql
sqlcmd -S localhost -i database\03_migracion_legacy.sql   :: opcional
```

### 2. Configuración
Edita `src/RuletaGame.Web/appsettings.json`:
- `ConnectionStrings:Kiosco` — la instancia local
- `Jwt:Clave` — **obligatorio**, mínimo 32 caracteres. El servicio no arranca
  con la clave de ejemplo. Genera una con:
  ```powershell
  [Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))
  ```
- `Kiosco:TokenPantalla` — el secreto que usa el pedestal para llamar a la API

### 3. Correr el backend
```
dotnet run --project src\RuletaGame.Web
```
Gestor en `http://localhost:5000`. Usuario `admin`, clave `Admin.2026`
(la pide cambiar en el primer ingreso).

### 4. Correr el kiosco
Copia los assets del proyecto legacy (ver `kiosco-electron/LEEME.md`), luego:
```
cd kiosco-electron
npm install
npm run dev
```

### 5. Publicar como servicio
```powershell
dotnet publish src\RuletaGame.Web -c Release -r win-x64 --self-contained ^
    -p:PublishSingleFile=true -o C:\S3K\Kiosco

sc create "S3KKiosco" binPath="C:\S3K\Kiosco\RuletaGame.Web.exe" start=auto
sc description "S3KKiosco" "Servicio de la ruleta del pedestal"

REM SQL Server tarda en levantar. Sin esta dependencia, el primer request
REM falla en cada reinicio de la PC.
sc config "S3KKiosco" depend=MSSQLSERVER
sc start "S3KKiosco"
```
> Si es SQL Express, la dependencia es `MSSQL$SQLEXPRESS`.

---

## Estructura

```
RuletaGame/
├── database/
│   ├── 01_esquema.sql              esquema nuevo
│   ├── 02_datos_iniciales.sql      configuración + usuario admin
│   └── 03_migracion_legacy.sql     trae los datos de S3K_NEWRULETA
│
├── src/
│   ├── RuletaGame.Domain/          entidades y enums. Cero dependencias.
│   ├── RuletaGame.Application/     casos de uso, interfaces, SorteoService
│   ├── RuletaGame.Infrastructure/  Dapper, seguridad, ImageSharp
│   └── RuletaGame.Web/             Gestor Ruleta + API del kiosco + SignalR
│
└── kiosco-electron/                app del pedestal (ver su LEEME.md)
```

### Pantallas del Gestor Ruleta

| Ruta | Qué hace |
|---|---|
| `/Login` | JWT en cookie HttpOnly, bloqueo tras 5 intentos |
| `/CambiarPassword` | Forzado en el primer ingreso |
| `/` | Tablero: cifras del día, consumo del tope, estado de pedestales |
| `/Ruletas` | Listado + alta/edición |
| `/Tajadas?ruletaId=N` | Premios: toggles de activo y "descuenta", probabilidad inline |
| `/Tajadas/Editar` | Alta/edición con subida de imagen |
| `/Pantallas` | Asignar ruleta al pedestal, estado en línea |
| `/Configuracion` | Parámetros + tope por fecha |
| `/Reportes` | Rango de fechas, filtros, exportación CSV |
| `/Usuarios` | Alta, roles, habilitar/deshabilitar |

---

## Decisiones que conviene conocer

**JWT en cookie HttpOnly.** Razor Pages navega con links y no puede mandar
cabeceras, así que un JWT en `localStorage` no sirve para el gestor. El token se
emite igual y se guarda en una cookie `HttpOnly` que el middleware lee en
`OnMessageReceived`. El gestor navega normal, el kiosco usa el header, y el
token queda fuera del alcance de cualquier XSS.

**CQRS sin mediador.** Cada caso de uso es una clase con un método
`EjecutarAsync` que se inyecta directo. Es CQRS de verdad sin MediatR: en un
proyecto de este tamaño el bus solo agrega indirección y una dependencia más.

**Dos tablas legacy unificadas.** `ruletadiatope` y `ruletatajadaganadora`
guardaban el mismo evento por duplicado. La segunda existía porque el reinicio
del ciclo anulaba sus filas y se habría perdido la contabilidad del tope. Ahora
cerrar un ciclo no toca las jugadas, así que sobra: hay una sola tabla `Jugada`
y el tope es un `SUM`.

**Ciclos explícitos.** El legacy "reiniciaba" con
`UPDATE ruletatajadaganadora SET TA_estado = 0` filtrando por fecha. Ahora
existe la tabla `Ciclo`, con un índice único que garantiza un solo ciclo abierto
por ruleta.

**El tope es global.** Suma todas las ruletas del día, igual que el legacy (su
`SUM` no filtraba por `TO_ruletaID`). Cada tajada tiene `AfectaTope`, que decide
si su monto descuenta: sirve para que un producto físico no consuma el
presupuesto de efectivo.

**La jugada se graba en el giro,** no al registrar el DNI. El legacy solo
guardaba las jugadas registradas, así que nunca se supo cuántos giros hubo.

---

## Lo que se corrigió del sistema original

| Original | Ahora |
|---|---|
| El sorteo corría en el navegador (manipulable desde F12) | Corre en el servidor, en `SorteoService` |
| `rand(0, lista.length)` inclusivo → 1 de cada N+1 giros ignoraba los porcentajes | Selección acumulada sobre el peso real |
| El ganador salía de todas las tajadas, no de las pendientes del ciclo | Sale de las candidatas del ciclo abierto |
| `if (total)` en vez de `if (total > 0)` → el tope no bloqueaba | Validado en el servidor, en cada giro |
| El tope solo se revisaba al abrir la pantalla | Se revisa antes de cada sorteo |
| Guardado en 3 pasos sin transacción | Una transacción, con rollback |
| SQL concatenado (inyección en casi todos los endpoints) | Todo parametrizado con Dapper |
| Sin autenticación | JWT + roles + bloqueo por intentos |
| `System.Drawing` (no corre en .NET moderno) | ImageSharp, salida WebP |
| Duración del giro con matemática oculta (3000 → 12 s) | Milisegundos reales |
| `config[0].CO_valor` sin validar → tumbaba la pantalla | Lectura tolerante con valores por defecto |
| Suma de porcentajes validada solo en el navegador | Validada en el servidor |
| Solo se guardaban las jugadas con DNI | Se guardan todas |

Para las diferencias del kiosco (60 fps, luces animadas, audio), ver
`kiosco-electron/LEEME.md`.

---

## Pendiente de decidir

1. **Qué hace el pedestal al llegar al tope.** Hoy devuelve el mensaje
   "Se alcanzó el tope de premios del día" y el kiosco lo muestra en la capa de
   error. Falta definir si debería quedarse en un estado especial o seguir
   jugando solo con premios que no descuentan.

2. **Superusuario de respaldo.** Está previsto en `appsettings.json`
   (`SuperUsuario:PasswordHash`, hoy deshabilitado) y sin implementar en el
   flujo de login. Lo dejé fuera del código compilado a propósito: una
   credencial incrustada no se puede rotar sin recompilar. Si prefieres
   incrustarla igual, dilo y lo muevo.

3. **Antiforgery en los handlers `OnPost`.** La cookie es `SameSite=Lax`, que
   mitiga CSRF, pero falta el token antiforgery. Vale agregarlo antes de
   producción.


---

## Nombres

Se usan tres nombres y cada uno significa algo distinto:

| Nombre | Qué es |
|---|---|
| **RuletaGame** | El proyecto completo (solución, namespaces) |
| **Gestor Ruleta** | La aplicación web de administración |
| **Kiosco** | La app Electron que corre en el pedestal |

El proyecto `RuletaGame.Web` hospeda las dos cosas: el Gestor Ruleta (Razor
Pages) y la API que consume el kiosco. Por eso su nombre es neutro.

> La base de datos sigue llamándose `S3K_KIOSCO`. La dejé así para no invalidar
> lo que ya cargaste. Si quieres renombrarla, cambia el `CREATE DATABASE`/`USE`
> de los tres scripts y el `ConnectionStrings:Kiosco` de `appsettings.json`.

---

## Vista previa de la rueda

En **Premios** (`/Tajadas?ruletaId=N`) hay un panel que dibuja la rueda tal como
quedará distribuida en el pedestal: los sectores, sus colores y sus textos. Se
actualiza al activar o desactivar un premio, así que puedes probar
combinaciones antes de enviar la ruleta.

**No necesita las imágenes del kiosco.** Dibuja formas propias a propósito, para
que funcione en cualquier PC donde abras el gestor. El arte real (las 11 piezas
PNG) lo pone la app Electron en el pedestal.

Desde **Pedestales** cada tarjeta tiene un botón *Ver la rueda* que lleva a esa
misma vista de la ruleta asignada.
