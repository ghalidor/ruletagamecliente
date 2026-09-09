using RuletaGame.Domain.Entities;

namespace RuletaGame.Application.Abstractions;

public interface IHasheadorPassword
{
    string Hashear(string password);
    bool   Verificar(string password, string hashAlmacenado);
}

public interface IGeneradorToken
{
    string Generar(Usuario usuario, out DateTime expiraEn);
}

public interface IServicioImagenes
{
    /// <summary>Guarda y redimensiona. Devuelve la ruta relativa web.</summary>
    Task<string> GuardarAsync(Stream archivo, string nombreOriginal, int ruletaId);
    void Eliminar(string? rutaRelativa);
}

/// <summary>
/// Empuja avisos al pedestal por WebSocket. La implementacion vive en WebHost
/// (SignalR); la capa Application solo conoce esta interfaz.
/// </summary>
public interface INotificadorKiosco
{
    Task CambiarRuletaAsync(string codigoPantalla, int? ruletaId);
    Task RecargarTajadasAsync(int ruletaId);
}
