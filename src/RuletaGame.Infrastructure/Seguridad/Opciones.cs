namespace RuletaGame.Infrastructure.Seguridad;

public sealed class OpcionesJwt
{
    public const string Seccion = "Jwt";

    public string Clave       { get; set; } = "";
    public string Emisor      { get; set; } = "RuletaGame";
    public string Audiencia   { get; set; } = "RuletaGame";
    public int    MinutosVida { get; set; } = 480;
}

/// <summary>
/// Superusuario de respaldo. Vive en appsettings y no en el codigo compilado,
/// para poder rotarlo sin recompilar: una credencial incrustada queda expuesta
/// para siempre si el binario se filtra.
/// </summary>
public sealed class OpcionesSuperUsuario
{
    public const string Seccion = "SuperUsuario";

    public bool   Habilitado    { get; set; } = true;
    public string NombreUsuario { get; set; } = "s3kroot";
    public string PasswordHash  { get; set; } = "";
}
