/* ============================================================================
   Migracion S3K_NEWRULETA (legacy)  ->  S3K_KIOSCO

   Ejecutar DESPUES de 01_esquema.sql y 02_datos_iniciales.sql.
   Corre esto primero sobre una copia.

   Decisiones de mapeo, todas explicitas:

   - ruletadiatope se DESCARTA. Registraba el mismo evento que
     ruletatajadaganadora (7 de sus columnas llevaban valores identicos,
     escritos en el mismo instante). Existia porque el reinicio del ciclo
     anulaba las filas de tajadaganadora y se habria perdido la contabilidad.
     Aca cerrar un ciclo no toca las jugadas, asi que sobra.

   - TA_estado = 0 (anulado por reinicio) se traduce a ConsumeCiclo = 0.

   - DE_porcentaje llega como varchar; lo no numerico pasa a NULL.

   - RU_hora no se migra: en el legacy no se usaba en ninguna logica, solo
     se mostraba en el listado.
   ============================================================================ */

USE [S3K_KIOSCO];
GO
SET XACT_ABORT ON;
BEGIN TRAN;

/* --- 1. Ruletas ---------------------------------------------------------- */
SET IDENTITY_INSERT dbo.Ruleta ON;

INSERT dbo.Ruleta (RuletaId, Nombre, Descripcion, Activo, FechaCreacion)
SELECT  r.RU_ruletaID,
        ISNULL(NULLIF(LTRIM(RTRIM(r.RU_nombre)), ''), CONCAT('Ruleta ', r.RU_ruletaID)),
        LEFT(CAST(r.RU_descripcion AS VARCHAR(MAX)), 500),
        CASE WHEN ISNULL(r.RU_estado, 0) = 1 THEN 1 ELSE 0 END,
        SYSDATETIME()
FROM    S3K_NEWRULETA.dbo.ruleta r
WHERE   NOT EXISTS (SELECT 1 FROM dbo.Ruleta d WHERE d.RuletaId = r.RU_ruletaID);

SET IDENTITY_INSERT dbo.Ruleta OFF;

/* --- 2. Tajadas ---------------------------------------------------------- */
SET IDENTITY_INSERT dbo.Tajada ON;

INSERT dbo.Tajada
    (TajadaId, RuletaId, Descripcion, Tipo, Monto, Probabilidad, Imagen,
     Orden, Activo, AfectaTope, FechaCreacion)
SELECT  d.DE_detalleID,
        d.DE_ruletaID,
        ISNULL(NULLIF(LTRIM(RTRIM(d.DE_descripcion)), ''), 'Premio'),
        CASE WHEN ISNULL(d.DE_tipo, 0) = 1 THEN 1 ELSE 0 END,
        ISNULL(d.DE_monto, 0),
        CASE WHEN TRY_CONVERT(DECIMAL(9,6), d.DE_porcentaje) BETWEEN 0 AND 1
             THEN TRY_CONVERT(DECIMAL(9,6), d.DE_porcentaje)
             ELSE NULL END,
        NULLIF(LTRIM(RTRIM(ISNULL(d.DE_imagen, ''))), ''),
        ROW_NUMBER() OVER (PARTITION BY d.DE_ruletaID ORDER BY d.DE_detalleID),
        CASE WHEN ISNULL(d.DE_estado, 0) = 1 THEN 1 ELSE 0 END,
        -- AfectaTope: el legacy no distinguia, todo sumaba al tope. Se migra
        -- en 1 para no alterar el comportamiento historico. Revisa despues las
        -- tajadas de tipo Producto y desmarcalas si corresponde.
        1,
        SYSDATETIME()
FROM    S3K_NEWRULETA.dbo.ruletadetalle d
JOIN    dbo.Ruleta r ON r.RuletaId = d.DE_ruletaID
WHERE   NOT EXISTS (SELECT 1 FROM dbo.Tajada t WHERE t.TajadaId = d.DE_detalleID);

SET IDENTITY_INSERT dbo.Tajada OFF;

/* --- 3. Ciclos historicos -------------------------------------------------
   El legacy no tenia ciclos. Reconstruimos uno por (ruleta, dia) a partir de
   las jugadas registradas, y los dejamos todos cerrados.
   ------------------------------------------------------------------------- */
INSERT dbo.Ciclo (RuletaId, Numero, FechaInicio, FechaCierre, Cerrado)
SELECT  x.RuletaId,
        ROW_NUMBER() OVER (PARTITION BY x.RuletaId ORDER BY x.Dia),
        CAST(x.Dia AS DATETIME2(0)),
        DATEADD(SECOND, 86399, CAST(x.Dia AS DATETIME2(0))),
        1
FROM (
    SELECT DISTINCT g.TA_ruletaID AS RuletaId, CAST(g.TA_fecha AS DATE) AS Dia
    FROM   S3K_NEWRULETA.dbo.ruletatajadaganadora g
    JOIN   dbo.Ruleta r ON r.RuletaId = g.TA_ruletaID
) x
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Ciclo c
    WHERE c.RuletaId = x.RuletaId AND CAST(c.FechaInicio AS DATE) = x.Dia
);

/* --- 4. Jugadas ---------------------------------------------------------- */
INSERT dbo.Jugada
(
    RuletaId, TajadaId, CicloId, PantallaId,
    DescripcionPremio, TipoPremio, MontoPremio, ProbabilidadAplicada, MontoTopeVigente,
    AnguloFinal, SorteoPonderado, CandidatasEnSorteo,
    Dni, Registrado, ConsumeCiclo, FechaJuego, FechaRegistro
)
SELECT
    g.TA_ruletaID,
    g.TA_detalleID,
    c.CicloId,
    NULL,
    ISNULL(NULLIF(LTRIM(RTRIM(g.TA_descripcionruleta)), ''), 'Premio'),
    CASE WHEN ISNULL(g.TA_tiporuleta, 0) = 1 THEN 1 ELSE 0 END,
    ISNULL(g.TA_montoruleta, 0),
    NULL,
    g.TA_montotope,
    0,          -- el legacy no guardaba el angulo del giro
    0,
    0,
    NULLIF(LTRIM(RTRIM(ISNULL(g.TA_dni, ''))), ''),
    CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(g.TA_dni, ''))), '') IS NULL THEN 0 ELSE 1 END,
    CASE WHEN ISNULL(g.TA_estado, 0) = 1
              AND NULLIF(LTRIM(RTRIM(ISNULL(g.TA_dni, ''))), '') IS NOT NULL
         THEN 1 ELSE 0 END,
    ISNULL(g.TA_fecha, g.TA_fecharegistro),
    CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(g.TA_dni, ''))), '') IS NULL
         THEN NULL ELSE g.TA_fecharegistro END
FROM  S3K_NEWRULETA.dbo.ruletatajadaganadora g
JOIN  dbo.Tajada t ON t.TajadaId = g.TA_detalleID
LEFT JOIN dbo.Ciclo c
       ON c.RuletaId = g.TA_ruletaID
      AND CAST(c.FechaInicio AS DATE) = CAST(g.TA_fecha AS DATE)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Jugada j);   -- solo si Jugada esta vacia

/* --- 5. Configuracion: traer los valores que estaban en uso -------------- */
UPDATE cfg
SET    Valor = l.CO_valor, FechaModificacion = SYSDATETIME()
FROM   dbo.Configuracion cfg
JOIN   S3K_NEWRULETA.dbo.ruletaconfiguracion l ON l.CO_tipo = 1
WHERE  cfg.Clave = 'MONEDA_SIMBOLO' AND LTRIM(RTRIM(l.CO_valor)) <> '';

UPDATE cfg
SET    Valor = l.CO_valor, FechaModificacion = SYSDATETIME()
FROM   dbo.Configuracion cfg
JOIN   S3K_NEWRULETA.dbo.ruletaconfiguracion l ON l.CO_tipo = 2
WHERE  cfg.Clave = 'TOPE_DIARIO_MONTO'
  AND  TRY_CONVERT(DECIMAL(18,2), l.CO_valor) IS NOT NULL;

/* CO_tipo = 3 pasa a milisegundos reales: valor * 4
   (en el legacy: valor * 0.06 frames / 15 fps = valor * 4 ms) */
UPDATE cfg
SET    Valor = CAST(TRY_CONVERT(INT, l.CO_valor) * 4 AS VARCHAR(250)),
       FechaModificacion = SYSDATETIME()
FROM   dbo.Configuracion cfg
JOIN   S3K_NEWRULETA.dbo.ruletaconfiguracion l ON l.CO_tipo = 3
WHERE  cfg.Clave = 'GIRO_DURACION_MS' AND TRY_CONVERT(INT, l.CO_valor) > 0;

/* --- 6. Salas ------------------------------------------------------------ */
INSERT dbo.Sala (Nombre, Activo)
SELECT s.nombre, CASE WHEN s.estado = 1 THEN 1 ELSE 0 END
FROM   S3K_NEWRULETA.dbo.sala s
WHERE  NOT EXISTS (SELECT 1 FROM dbo.Sala d WHERE d.Nombre = s.nombre);

/* --- 7. Reiniciar los contadores IDENTITY ------------------------------- */
DECLARE @maxR INT = (SELECT ISNULL(MAX(RuletaId), 0) FROM dbo.Ruleta);
DECLARE @maxT INT = (SELECT ISNULL(MAX(TajadaId), 0) FROM dbo.Tajada);
IF @maxR > 0 DBCC CHECKIDENT ('dbo.Ruleta', RESEED, @maxR) WITH NO_INFOMSGS;
IF @maxT > 0 DBCC CHECKIDENT ('dbo.Tajada', RESEED, @maxT) WITH NO_INFOMSGS;

COMMIT;
GO

/* --- Verificacion ------------------------------------------------------- */
SELECT 'Ruleta' t, COUNT(*) n FROM dbo.Ruleta
UNION ALL SELECT 'Tajada', COUNT(*) FROM dbo.Tajada
UNION ALL SELECT 'Ciclo',  COUNT(*) FROM dbo.Ciclo
UNION ALL SELECT 'Jugada', COUNT(*) FROM dbo.Jugada
UNION ALL SELECT 'Sala',   COUNT(*) FROM dbo.Sala;
GO
