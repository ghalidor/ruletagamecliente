using RuletaGame.Application.Abstractions;
using RuletaGame.Application.Common;
using RuletaGame.Application.Dtos;

namespace RuletaGame.Application.Commands;

public sealed record LoginCommand(string NombreUsuario, string Password);

public sealed class LoginCommandHandler(
    IUsuarioRepositorio usuarios,
    IHasheadorPassword  hasheador,
    IGeneradorToken     generador)
{
    private const int MaxIntentos    = 5;
    private const int MinutosBloqueo = 10;

    public async Task<Resultado<LoginDto>> EjecutarAsync(LoginCommand comando)
    {
        // Mensaje unico para usuario inexistente y clave incorrecta: no le
        // decimos a un atacante cuales usuarios existen.
        const string generico = "Usuario o contrasena incorrectos.";

        var usuario = await usuarios.ObtenerPorNombreAsync((comando.NombreUsuario ?? "").Trim());
        if (usuario is null)  return Resultado<LoginDto>.Fallo(generico);
        if (!usuario.Activo)  return Resultado<LoginDto>.Fallo("El usuario esta deshabilitado.");

        if (usuario.EstaBloqueado)
        {
            int restan = (int)Math.Ceiling((usuario.BloqueadoHasta!.Value - DateTime.Now).TotalMinutes);
            return Resultado<LoginDto>.Fallo($"Demasiados intentos. Vuelve a probar en {restan} minutos.");
        }

        if (!hasheador.Verificar(comando.Password ?? "", usuario.PasswordHash))
        {
            int intentos = usuario.IntentosFallidos + 1;
            DateTime? bloqueo = intentos >= MaxIntentos ? DateTime.Now.AddMinutes(MinutosBloqueo) : null;

            await usuarios.RegistrarFalloAsync(usuario.UsuarioId, intentos, bloqueo);

            return Resultado<LoginDto>.Fallo(bloqueo is null
                ? generico
                : $"Demasiados intentos. La cuenta queda bloqueada {MinutosBloqueo} minutos.");
        }

        await usuarios.RegistrarAccesoAsync(usuario.UsuarioId);

        string token = generador.Generar(usuario, out DateTime expiraEn);

        return Resultado<LoginDto>.Ok(new LoginDto
        {
            Token               = token,
            ExpiraEn            = expiraEn,
            NombreUsuario       = usuario.NombreUsuario,
            NombreCompleto      = usuario.NombreCompleto,
            Rol                 = usuario.Rol,
            DebeCambiarPassword = usuario.DebeCambiarPassword
        });
    }
}
