using System.Security.Cryptography;
using RuletaGame.Application.Abstractions;

namespace RuletaGame.Infrastructure.Seguridad;

/// <summary>
/// PBKDF2-SHA256. Formato almacenado: {iteraciones}.{salt}.{hash} en base64.
/// Sin dependencias externas y con comparacion en tiempo constante.
/// </summary>
public sealed class HasheadorPassword : IHasheadorPassword
{
    private const int Iteraciones = 100_000;
    private const int TamanoSalt  = 16;
    private const int TamanoHash  = 32;

    public string Hashear(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(TamanoSalt);
        byte[] hash = Derivar(password, salt, Iteraciones);
        return $"{Iteraciones}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verificar(string password, string hashAlmacenado)
    {
        var partes = (hashAlmacenado ?? "").Split('.');
        if (partes.Length != 3) return false;
        if (!int.TryParse(partes[0], out int iteraciones) || iteraciones <= 0) return false;

        try
        {
            byte[] salt     = Convert.FromBase64String(partes[1]);
            byte[] esperado = Convert.FromBase64String(partes[2]);
            byte[] actual   = Derivar(password, salt, iteraciones);
            return CryptographicOperations.FixedTimeEquals(actual, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Derivar(string password, byte[] salt, int iteraciones) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iteraciones, HashAlgorithmName.SHA256, TamanoHash);
}
