/* ============================================================================
   S3K Kiosco Ruleta - Esquema
   Base de datos nueva. NO sobrescribe la legacy (S3K_NEWRULETA).
   ============================================================================ */

IF DB_ID('S3K_KIOSCO') IS NULL
    CREATE DATABASE [S3K_KIOSCO];
GO
USE [S3K_KIOSCO];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------------
   Usuario
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Usuario
(
    UsuarioId               INT             IDENTITY(1,1) NOT NULL,
    NombreUsuario           VARCHAR(60)     NOT NULL,
    NombreCompleto          VARCHAR(150)    NOT NULL,
    PasswordHash            VARCHAR(500)    NOT NULL,
    Rol                     VARCHAR(20)     NOT NULL,   -- SUPERADMIN | ADMIN | OPERADOR
    Activo                  BIT             NOT NULL CONSTRAINT DF_Usuario_Activo   DEFAULT (1),
    DebeCambiarPassword     BIT             NOT NULL CONSTRAINT DF_Usuario_CambPwd  DEFAULT (0),
    UltimoAcceso            DATETIME2(0)    NULL,
    IntentosFallidos        INT             NOT NULL CONSTRAINT DF_Usuario_Intentos DEFAULT (0),
    BloqueadoHasta          DATETIME2(0)    NULL,
    FechaCreacion           DATETIME2(0)    NOT NULL CONSTRAINT DF_Usuario_FCrea    DEFAULT (SYSDATETIME()),
    FechaModificacion       DATETIME2(0)    NULL,
    CONSTRAINT PK_Usuario PRIMARY KEY CLUSTERED (UsuarioId),
    CONSTRAINT UQ_Usuario_Nombre UNIQUE (NombreUsuario),
    CONSTRAINT CK_Usuario_Rol CHECK (Rol IN ('SUPERADMIN','ADMIN','OPERADOR'))
);
GO

/* ---------------------------------------------------------------------------
   Sala
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Sala
(
    SalaId              INT             IDENTITY(1,1) NOT NULL,
    Nombre              VARCHAR(120)    NOT NULL,
    Direccion           VARCHAR(250)    NULL,
    Activo              BIT             NOT NULL CONSTRAINT DF_Sala_Activo DEFAULT (1),
    FechaCreacion       DATETIME2(0)    NOT NULL CONSTRAINT DF_Sala_FCrea  DEFAULT (SYSDATETIME()),
    FechaModificacion   DATETIME2(0)    NULL,
    CONSTRAINT PK_Sala PRIMARY KEY CLUSTERED (SalaId)
);
GO

/* ---------------------------------------------------------------------------
   Ruleta
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Ruleta
(
    RuletaId            INT             IDENTITY(1,1) NOT NULL,
    Nombre              VARCHAR(150)    NOT NULL,
    Descripcion         VARCHAR(500)    NULL,
    Activo              BIT             NOT NULL CONSTRAINT DF_Ruleta_Activo DEFAULT (1),
    UsuarioCreacionId   INT             NULL,
    FechaCreacion       DATETIME2(0)    NOT NULL CONSTRAINT DF_Ruleta_FCrea  DEFAULT (SYSDATETIME()),
    FechaModificacion   DATETIME2(0)    NULL,
    CONSTRAINT PK_Ruleta PRIMARY KEY CLUSTERED (RuletaId),
    CONSTRAINT FK_Ruleta_Usuario FOREIGN KEY (UsuarioCreacionId) REFERENCES dbo.Usuario(UsuarioId)
);
GO
CREATE INDEX IX_Ruleta_Activo ON dbo.Ruleta(Activo) INCLUDE (Nombre);
GO

/* ---------------------------------------------------------------------------
   Tajada  (los premios que se dibujan en la rueda)
   Probabilidad: fraccion. 0.25 = 25%. NULL o 0 = sin peso.
   Orden: define la posicion en la rueda Y el color (ciclo de 5 colores).
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Tajada
(
    TajadaId            INT             IDENTITY(1,1) NOT NULL,
    RuletaId            INT             NOT NULL,
    Descripcion         VARCHAR(200)    NOT NULL,
    Tipo                TINYINT         NOT NULL,   -- 0 = MONTO, 1 = PRODUCTO
    Monto               DECIMAL(18,2)   NOT NULL CONSTRAINT DF_Tajada_Monto  DEFAULT (0),
    Probabilidad        DECIMAL(9,6)    NULL,
    Imagen              VARCHAR(300)    NULL,
    Orden               INT             NOT NULL CONSTRAINT DF_Tajada_Orden  DEFAULT (0),
    Activo              BIT             NOT NULL CONSTRAINT DF_Tajada_Activo DEFAULT (1),

    -- Si esta en 0, el premio se entrega pero NO descuenta del tope diario.
    -- Sirve para los productos: un llavero no deberia consumir el presupuesto
    -- de efectivo. El sistema legacy no distinguia y todo sumaba al tope.
    AfectaTope          BIT             NOT NULL CONSTRAINT DF_Tajada_Tope   DEFAULT (1),

    FechaCreacion       DATETIME2(0)    NOT NULL CONSTRAINT DF_Tajada_FCrea  DEFAULT (SYSDATETIME()),
    FechaModificacion   DATETIME2(0)    NULL,
    CONSTRAINT PK_Tajada PRIMARY KEY CLUSTERED (TajadaId),
    CONSTRAINT FK_Tajada_Ruleta FOREIGN KEY (RuletaId) REFERENCES dbo.Ruleta(RuletaId),
    CONSTRAINT CK_Tajada_Tipo CHECK (Tipo IN (0,1)),
    CONSTRAINT CK_Tajada_Prob CHECK (Probabilidad IS NULL OR (Probabilidad >= 0 AND Probabilidad <= 1))
);
GO
CREATE INDEX IX_Tajada_Ruleta ON dbo.Tajada(RuletaId, Activo)
    INCLUDE (Orden, Descripcion, Tipo, Monto, Probabilidad, Imagen, AfectaTope);
GO

/* ---------------------------------------------------------------------------
   Ciclo
   Reemplaza el truco legacy de "UPDATE ... SET TA_estado = 0" por fecha.
   Un ciclo agrupa las jugadas registradas hasta agotar las tajadas.
   El indice filtrado garantiza un solo ciclo abierto por ruleta.
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Ciclo
(
    CicloId         INT             IDENTITY(1,1) NOT NULL,
    RuletaId        INT             NOT NULL,
    Numero          INT             NOT NULL,
    FechaInicio     DATETIME2(0)    NOT NULL CONSTRAINT DF_Ciclo_FIni    DEFAULT (SYSDATETIME()),
    FechaCierre     DATETIME2(0)    NULL,
    Cerrado         BIT             NOT NULL CONSTRAINT DF_Ciclo_Cerrado DEFAULT (0),
    CONSTRAINT PK_Ciclo PRIMARY KEY CLUSTERED (CicloId),
    CONSTRAINT FK_Ciclo_Ruleta FOREIGN KEY (RuletaId) REFERENCES dbo.Ruleta(RuletaId)
);
GO
CREATE UNIQUE INDEX UX_Ciclo_Abierto ON dbo.Ciclo(RuletaId) WHERE Cerrado = 0;
GO

/* ---------------------------------------------------------------------------
   Pantalla  (cada pedestal)
   RuletaAsignadaId es lo que la tablet cambia y lo que el kiosco muestra.
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Pantalla
(
    PantallaId          INT             IDENTITY(1,1) NOT NULL,
    Codigo              VARCHAR(40)     NOT NULL,   -- el kiosco se identifica con esto
    Nombre              VARCHAR(120)    NOT NULL,
    SalaId              INT             NULL,
    RuletaAsignadaId    INT             NULL,
    UltimaConexion      DATETIME2(0)    NULL,
    Activo              BIT             NOT NULL CONSTRAINT DF_Pantalla_Activo DEFAULT (1),
    FechaCreacion       DATETIME2(0)    NOT NULL CONSTRAINT DF_Pantalla_FCrea  DEFAULT (SYSDATETIME()),
    FechaModificacion   DATETIME2(0)    NULL,
    CONSTRAINT PK_Pantalla PRIMARY KEY CLUSTERED (PantallaId),
    CONSTRAINT UQ_Pantalla_Codigo UNIQUE (Codigo),
    CONSTRAINT FK_Pantalla_Sala   FOREIGN KEY (SalaId)           REFERENCES dbo.Sala(SalaId),
    CONSTRAINT FK_Pantalla_Ruleta FOREIGN KEY (RuletaAsignadaId) REFERENCES dbo.Ruleta(RuletaId)
);
GO

/* ---------------------------------------------------------------------------
   Jugada
   Unifica las dos tablas legacy (ruletadiatope + ruletatajadaganadora).
   El tope diario ahora es un SUM sobre esta tabla, no una tabla aparte:
   cerrar un ciclo ya no modifica las jugadas, asi que la suma no se pierde.

   Otra diferencia importante: aca la jugada se graba en el GIRO, no al
   registrar el DNI. El sistema legacy solo guardaba las jugadas registradas,
   asi que nunca se supo cuantos giros hubo en realidad.
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Jugada
(
    JugadaId                BIGINT          IDENTITY(1,1) NOT NULL,
    RuletaId                INT             NOT NULL,
    TajadaId                INT             NOT NULL,
    CicloId                 INT             NULL,
    PantallaId              INT             NULL,

    -- Copia del premio al momento del giro (historico inmutable)
    DescripcionPremio       VARCHAR(200)    NOT NULL,
    TipoPremio              TINYINT         NOT NULL,
    MontoPremio             DECIMAL(18,2)   NOT NULL,
    ProbabilidadAplicada    DECIMAL(9,6)    NULL,
    MontoTopeVigente        DECIMAL(18,2)   NULL,

    -- Trazabilidad del sorteo
    AnguloFinal             DECIMAL(10,4)   NOT NULL,
    SorteoPonderado         BIT             NOT NULL CONSTRAINT DF_Jugada_Ponderado  DEFAULT (0),
    CandidatasEnSorteo      INT             NOT NULL CONSTRAINT DF_Jugada_Candidatas DEFAULT (0),

    -- Registro del cliente
    Dni                     VARCHAR(20)     NULL,
    Registrado              BIT             NOT NULL CONSTRAINT DF_Jugada_Registrado DEFAULT (0),
    ConsumeCiclo            BIT             NOT NULL CONSTRAINT DF_Jugada_Consume    DEFAULT (0),

    FechaJuego              DATETIME2(0)    NOT NULL CONSTRAINT DF_Jugada_FJuego     DEFAULT (SYSDATETIME()),
    FechaRegistro           DATETIME2(0)    NULL,
    CONSTRAINT PK_Jugada PRIMARY KEY CLUSTERED (JugadaId),
    CONSTRAINT FK_Jugada_Ruleta   FOREIGN KEY (RuletaId)   REFERENCES dbo.Ruleta(RuletaId),
    CONSTRAINT FK_Jugada_Tajada   FOREIGN KEY (TajadaId)   REFERENCES dbo.Tajada(TajadaId),
    CONSTRAINT FK_Jugada_Ciclo    FOREIGN KEY (CicloId)    REFERENCES dbo.Ciclo(CicloId),
    CONSTRAINT FK_Jugada_Pantalla FOREIGN KEY (PantallaId) REFERENCES dbo.Pantalla(PantallaId)
);
GO
CREATE INDEX IX_Jugada_Fecha        ON dbo.Jugada(FechaJuego DESC) INCLUDE (RuletaId, TajadaId, MontoPremio, Registrado);
CREATE INDEX IX_Jugada_CicloConsumo ON dbo.Jugada(CicloId, ConsumeCiclo) INCLUDE (TajadaId);
CREATE INDEX IX_Jugada_Dni          ON dbo.Jugada(Dni) WHERE Dni IS NOT NULL;
GO

/* ---------------------------------------------------------------------------
   Configuracion
   Clave de texto en vez del CO_tipo numerico del legacy (1 = moneda,
   2 = tope, 3 = tiempo), que no se entendia sin leer el codigo.
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Configuracion
(
    Clave               VARCHAR(60)     NOT NULL,
    Valor               VARCHAR(250)    NOT NULL,
    Descripcion         VARCHAR(250)    NOT NULL,
    TipoDato            VARCHAR(20)     NOT NULL,   -- TEXTO | ENTERO | DECIMAL | BOOL
    FechaModificacion   DATETIME2(0)    NULL,
    CONSTRAINT PK_Configuracion PRIMARY KEY CLUSTERED (Clave),
    CONSTRAINT CK_Configuracion_Tipo CHECK (TipoDato IN ('TEXTO','ENTERO','DECIMAL','BOOL'))
);
GO

/* ---------------------------------------------------------------------------
   TopeDiario
   El tope base vive en Configuracion (TOPE_DIARIO_MONTO) y aplica todos los
   dias. Esta tabla lo pisa para una fecha puntual: subirlo un feriado, bajarlo
   un dia flojo. Si no hay fila para el dia, manda el valor base.
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.TopeDiario
(
    Fecha           DATE            NOT NULL,
    Monto           DECIMAL(18,2)   NOT NULL,
    Nota            VARCHAR(250)    NULL,
    UsuarioId       INT             NULL,
    FechaCreacion   DATETIME2(0)    NOT NULL CONSTRAINT DF_TopeDiario_FCrea DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_TopeDiario PRIMARY KEY CLUSTERED (Fecha),
    CONSTRAINT CK_TopeDiario_Monto CHECK (Monto >= 0),
    CONSTRAINT FK_TopeDiario_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario(UsuarioId)
);
GO

/* ---------------------------------------------------------------------------
   Vista: consumo del tope por dia.
   El tope es GLOBAL (todas las ruletas juntas), igual que en el legacy.
   Solo cuentan las jugadas cuya tajada tiene AfectaTope = 1.
   --------------------------------------------------------------------------- */
CREATE VIEW dbo.vw_ConsumoTope
AS
SELECT
    CAST(j.FechaJuego AS DATE)                                  AS Fecha,
    COUNT(*)                                                    AS JugadasTotales,
    SUM(CASE WHEN t.AfectaTope = 1 THEN 1 ELSE 0 END)           AS JugadasQueDescuentan,
    ISNULL(SUM(CASE WHEN t.AfectaTope = 1
                    THEN j.MontoPremio ELSE 0 END), 0)          AS MontoQueDescuenta,
    ISNULL(SUM(j.MontoPremio), 0)                               AS MontoTotalEntregado
FROM dbo.Jugada j
JOIN dbo.Tajada t ON t.TajadaId = j.TajadaId
GROUP BY CAST(j.FechaJuego AS DATE);
GO
