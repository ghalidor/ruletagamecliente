using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RuletaGame.Application.Abstractions;
using RuletaGame.Infrastructure;
using RuletaGame.Infrastructure.Seguridad;
using RuletaGame.Infrastructure.Servicios;
using RuletaGame.Web;
using RuletaGame.Web.Api;
using RuletaGame.Web.Hubs;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;

// Solo como servicio de Windows hay que forzar la raiz: Windows lo arranca
// desde System32 y no encontraria wwwroot ni appsettings.
//
// En desarrollo se deja en null, porque apuntar a bin\Debug haria que las
// imagenes subidas se guarden ahi en vez de en la carpeta del proyecto.
//
// ContentRootPath es init-only, asi que se decide antes de construir.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService()
        ? AppContext.BaseDirectory
        : null
});

// ---------------------------------------------------------------- Servicio
// Con esto el mismo ejecutable corre como consola (para depurar) y como
// servicio de Windows (en el pedestal). No hay dos compilaciones distintas.
builder.Host.UseWindowsService(opciones => opciones.ServiceName = "S3K Kiosco Ruleta");

// -------------------------------------------------------------------- Logs
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "kiosco-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// --------------------------------------------------------------- Servicios
builder.Services.AgregarInfraestructura(builder.Configuration);
builder.Services.AgregarCasosDeUso();
builder.Services.AddScoped<INotificadorKiosco, NotificadorKiosco>();

// El servicio de imagenes necesita saber donde esta wwwroot.
builder.Services.AddSingleton<IServicioImagenes>(proveedor =>
{
    var entorno = proveedor.GetRequiredService<IWebHostEnvironment>();

    // WebRootPath viene null si la carpeta wwwroot no existe todavia en la
    // salida de compilacion. Se arma la ruta a mano y se crea, para que la
    // subida de imagenes funcione desde el primer arranque.
    string raiz = string.IsNullOrWhiteSpace(entorno.WebRootPath)
        ? Path.Combine(entorno.ContentRootPath, "wwwroot")
        : entorno.WebRootPath;

    Directory.CreateDirectory(raiz);
    return new ServicioImagenes(raiz);
});

builder.Services.AddRazorPages(opciones =>
{
    // Todo el gestor exige sesion; solo el login y el cambio de clave quedan libres.
    opciones.Conventions.AuthorizeFolder("/");
    opciones.Conventions.AllowAnonymousToPage("/Login");
    opciones.Conventions.AllowAnonymousToPage("/Error");
});

builder.Services.AddSignalR();

// Manda los clientes al sistema externo fuera del hilo del registro: el
// cliente esta frente al pedestal y no puede esperar a un tercero.
builder.Services.AddHostedService<ProcesadorEnvios>();

// El kiosco Electron carga sus paginas desde file://, asi que para el servidor
// es un origen distinto. Sin CORS, la negociacion de SignalR se rechaza y el
// pedestal nunca recibe el aviso de cambio de ruleta.
//
// AllowCredentials es obligatorio para SignalR, y exige origenes explicitos:
// no se puede combinar con AllowAnyOrigin.
builder.Services.AddCors(opciones =>
    opciones.AddPolicy("Kiosco", politica => politica
        .SetIsOriginAllowed(_ => true)   // incluye el origen null de file://
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// Los handlers OnPost de Razor Pages validan el token antiforgery por defecto.
// El gestor llama por fetch, que no puede mandar un campo de formulario, asi
// que se acepta el token por cabecera. Sin esto ningun POST del gestor guarda.
builder.Services.AddAntiforgery(opciones =>
    opciones.HeaderName = "RequestVerificationToken");

// ------------------------------------------------------------ Autenticacion
var opcionesJwt = builder.Configuration.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>()
    ?? throw new InvalidOperationException("Falta la seccion Jwt en appsettings.json.");

if (opcionesJwt.Clave.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Clave debe tener al menos 32 caracteres. Genera una aleatoria antes de publicar.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = opcionesJwt.Emisor,
            ValidAudience            = opcionesJwt.Audiencia,
            IssuerSigningKey         = new SymmetricSecurityKey(
                                          Encoding.UTF8.GetBytes(opcionesJwt.Clave)),
            ClockSkew                = TimeSpan.FromMinutes(1)
        };

        opciones.Events = new JwtBearerEvents
        {
            // Razor Pages navega con links y no puede mandar cabeceras, asi que
            // el token se lee de la cookie HttpOnly. El kiosco sigue usando el
            // header normal.
            OnMessageReceived = contexto =>
            {
                if (string.IsNullOrEmpty(contexto.Token) &&
                    contexto.Request.Cookies.TryGetValue(EndpointsApi.CookieToken, out var token))
                {
                    contexto.Token = token;
                }
                return Task.CompletedTask;
            },

            // Sin esto, una sesion vencida en el gestor devuelve un 401 en
            // blanco en vez de mandar al login.
            OnChallenge = contexto =>
            {
                if (!contexto.Request.Path.StartsWithSegments("/api"))
                {
                    contexto.HandleResponse();
                    contexto.Response.Redirect(
                        $"/Login?volverA={Uri.EscapeDataString(contexto.Request.Path)}");
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ------------------------------------------------------------------ Pipeline
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

// La carpeta de premios se crea al vuelo cuando el operador sube la primera
// imagen. Si no existe al arrancar, el proveedor de archivos estaticos no la
// toma y las imagenes subidas responden 404 aunque esten en disco.
Directory.CreateDirectory(
    Path.Combine(app.Environment.ContentRootPath, "wwwroot", "premios"));

app.UseStaticFiles();

app.UseSerilogRequestLogging(opciones =>
{
    // El latido del kiosco entra cada 30 segundos: no ensuciamos el log con eso.
    opciones.GetLevel = (contexto, _, _) =>
        contexto.Request.Path.StartsWithSegments("/api/kiosco/latido")
            ? Serilog.Events.LogEventLevel.Verbose
            : Serilog.Events.LogEventLevel.Information;
});

app.UseRouting();
app.UseCors("Kiosco");   // va despues de UseRouting y antes de la autorizacion
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapearEndpoints();
app.MapearTerminalWeb();

// La ruleta en navegador. Es el mismo frontend del pedestal, servido por HTTP
// en vez de empaquetado en Electron: asi se abre desde cualquier tablet o
// celular que alcance este servidor.
app.MapGet("/juego", () => Results.File(
    Path.Combine(app.Environment.WebRootPath, "juego", "index.html"), "text/html"))
   .AllowAnonymous();
app.MapHub<KioscoHub>("/hub/kiosco").RequireCors("Kiosco");

app.MapGet("/salud", () => Results.Ok(new { estado = "ok", hora = DateTime.Now }))
   .AllowAnonymous();

await SembradorInicial.EjecutarAsync(app.Services);

try
{
    Log.Information("S3K Kiosco iniciando");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "El servicio no pudo arrancar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
