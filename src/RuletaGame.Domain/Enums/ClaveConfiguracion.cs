namespace RuletaGame.Domain.Enums;

/// <summary>
/// Claves de la tabla Configuracion. Reemplaza el CO_tipo numerico del sistema
/// legacy (1 = moneda, 2 = tope, 3 = tiempo), que no se entendia sin el codigo.
/// </summary>
public static class ClaveConfiguracion {
    public const string MonedaSimbolo = "MONEDA_SIMBOLO";
    public const string TopeDiarioMonto = "TOPE_DIARIO_MONTO";
    public const string GiroDuracionMs = "GIRO_DURACION_MS";
    public const string GiroVueltas = "GIRO_VUELTAS";
    public const string StandbyTimeoutSegundos = "STANDBY_TIMEOUT_SEGUNDOS";
    public const string RegistroTimeoutSegundos = "REGISTRO_TIMEOUT_SEGUNDOS";
    public const string CicloHabilitado = "CICLO_HABILITADO";

    /// <summary>Si esta en 0, el pedestal no ofrece registrarse al ganador.</summary>
    public const string ClientePideDatos = "CLIENTE_PIDE_DATOS";
}
