namespace RuletaGame.Application.Dtos;

/// <summary>Lo que el kiosco necesita para animar el giro y mostrar el premio.</summary>
public sealed class GiroDto {
    public long JugadaId { get; set; }
    public int TajadaId { get; set; }
    public decimal AnguloFinal { get; set; }
    public int DuracionMs { get; set; }
    public string TextoPremio { get; set; } = "";
    public string DescripcionPremio { get; set; } = "";
    public int TipoPremio { get; set; }
    public decimal MontoPremio { get; set; }
    public bool CierraCiclo { get; set; }

    /// <summary>Huella vigente. La terminal la guarda para el proximo giro.</summary>
    public string VersionRueda { get; set; } = "";
}

/// <summary>Todo lo que el kiosco necesita para dibujar la rueda y comportarse.</summary>
public sealed class RuletaKioscoDto {
    public int RuletaId { get; set; }
    public string Nombre { get; set; } = "";

    public List<TajadaKioscoDto> Tajadas { get; set; } = [];

    public string MonedaSimbolo { get; set; } = "S/.";
    public int GiroDuracionMs { get; set; }
    public int GiroVueltas { get; set; }
    public int StandbyTimeoutSegundos { get; set; }
    public int RegistroTimeoutSegundos { get; set; }
    /// <summary>Si es false, el pedestal no ofrece registrarse al ganador.</summary>
    public bool PideDatosCliente { get; set; } = true;

    /// <summary>Reglas de cada tipo de documento, para validar en el kiosco.</summary>
    public List<TipoDocumentoDto> TiposDocumento { get; set; } = [];

    public bool CorreoObligatorio { get; set; }
    public bool TelefonoObligatorio { get; set; }

    /// <summary>
    /// Huella de esta configuracion. Viaja en cada giro para que el servidor
    /// detecte si la terminal quedo con una rueda vieja.
    /// </summary>
    public string VersionRueda { get; set; } = "";
}

public sealed class TipoDocumentoDto {
    public int Valor { get; set; }
    public string Nombre { get; set; } = "";

    /// <summary>Version corta para los botones del pedestal.</summary>
    public string NombreCorto { get; set; } = "";
    public bool SoloNumeros { get; set; }
    public int Minimo { get; set; }
    public int Maximo { get; set; }
}

public sealed class TajadaKioscoDto {
    public int TajadaId { get; set; }
    public string Texto { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public int Tipo { get; set; }
    public decimal Monto { get; set; }
    public string? Imagen { get; set; }
    public string Color { get; set; } = "";
    public int Orden { get; set; }
}

public sealed class LoginDto {
    public string Token { get; set; } = "";
    public DateTime ExpiraEn { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string NombreCompleto { get; set; } = "";
    public string Rol { get; set; } = "";
    public bool DebeCambiarPassword { get; set; }
}

/// <summary>Cifras del tablero principal del gestor.</summary>
public sealed class ResumenDto {
    public int RuletasActivas { get; set; }
    public int JugadasHoy { get; set; }

    /// <summary>Todo lo entregado hoy, incluidos los premios que no descuentan.</summary>
    public decimal EntregadoHoy { get; set; }

    /// <summary>Solo lo que descuenta del tope (tajadas con AfectaTope = 1).</summary>
    public decimal ConsumoTope { get; set; }

    public decimal TopeDiario { get; set; }
    public bool TopeEsExcepcion { get; set; }
    public string? TopeNota { get; set; }
    public string MonedaSimbolo { get; set; } = "S/.";
    public int PantallasEnLinea { get; set; }
    public int PantallasTotales { get; set; }

    public List<PantallaResumenDto> Pantallas { get; set; } = [];
}

public sealed class PantallaResumenDto {
    public int PantallaId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? NombreRuleta { get; set; }
    public bool EnLinea { get; set; }
}
