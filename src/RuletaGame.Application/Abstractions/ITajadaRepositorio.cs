using RuletaGame.Domain.Entities;

namespace RuletaGame.Application.Abstractions;

public interface ITajadaRepositorio
{
    Task<IReadOnlyList<Tajada>> ListarPorRuletaAsync(int ruletaId, bool soloActivas = false);
    Task<Tajada?> ObtenerAsync(int tajadaId);
    Task<int>  CrearAsync(Tajada tajada);
    Task<bool> ActualizarAsync(Tajada tajada);
    Task<bool> EliminarAsync(int tajadaId);
    Task<bool> CambiarActivoAsync(int tajadaId, bool activo);
    Task<bool> ActualizarProbabilidadAsync(int tajadaId, decimal? probabilidad);
    Task<decimal> SumaProbabilidadesAsync(int ruletaId, int? excluirTajadaId = null);
    Task<int> SiguienteOrdenAsync(int ruletaId);
}
