namespace RuletaGame.Domain.Entities;

public sealed class Usuario
{
    public int       UsuarioId           { get; set; }
    public string    NombreUsuario       { get; set; } = "";
    public string    NombreCompleto      { get; set; } = "";
    public string    PasswordHash        { get; set; } = "";
    public string    Rol                 { get; set; } = "";
    public bool      Activo              { get; set; }
    public bool      DebeCambiarPassword { get; set; }
    public DateTime? UltimoAcceso        { get; set; }
    public int       IntentosFallidos    { get; set; }
    public DateTime? BloqueadoHasta      { get; set; }
    public DateTime  FechaCreacion       { get; set; }
    public DateTime? FechaModificacion   { get; set; }

    public bool EstaBloqueado => BloqueadoHasta.HasValue && BloqueadoHasta > DateTime.Now;
}
