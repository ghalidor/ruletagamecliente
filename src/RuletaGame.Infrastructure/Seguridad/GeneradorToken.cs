using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;

namespace RuletaGame.Infrastructure.Seguridad;

public sealed class GeneradorToken(IOptions<OpcionesJwt> opciones) : IGeneradorToken
{
    private readonly OpcionesJwt _op = opciones.Value;

    public string Generar(Usuario usuario, out DateTime expiraEn)
    {
        expiraEn = DateTime.UtcNow.AddMinutes(_op.MinutosVida);

        var claims = new List<Claim>
        {
            new("sub",                      usuario.UsuarioId.ToString()),
            new(ClaimTypes.NameIdentifier,  usuario.UsuarioId.ToString()),
            new(ClaimTypes.Name,            usuario.NombreUsuario),
            new(ClaimTypes.Role,            usuario.Rol),
            new("nombre_completo",          usuario.NombreCompleto),
            new("debe_cambiar",             usuario.DebeCambiarPassword ? "1" : "0")
        };

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_op.Clave)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             _op.Emisor,
            audience:           _op.Audiencia,
            claims:             claims,
            expires:            expiraEn,
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
