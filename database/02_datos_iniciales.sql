USE [S3K_KIOSCO];
GO

/* ---------------------------------------------------------------------------
   Configuracion inicial

   OJO con GIRO_DURACION_MS: aca son milisegundos REALES.
   En el sistema legacy el valor 3000 producia un giro de 12 segundos, porque
   la duracion se calculaba como valor * 0.06 frames / 15 fps = valor * 4 ms.
   La etiqueta decia "1 sec = 1000" y nadie lo sabia.
   --------------------------------------------------------------------------- */
MERGE dbo.Configuracion AS destino
USING (VALUES
    ('MONEDA_SIMBOLO',            'S/.',  'Simbolo que se antepone a los premios de tipo monto',                'TEXTO'),
    ('TOPE_DIARIO_MONTO',         '5000', 'Tope base global de premios por dia. 0 = sin tope',                  'DECIMAL'),
    ('GIRO_DURACION_MS',          '8000', 'Duracion real del giro en milisegundos',                             'ENTERO'),
    ('GIRO_VUELTAS',              '16',   'Vueltas completas antes de frenar. Debe ser entero',                 'ENTERO'),
    ('STANDBY_TIMEOUT_SEGUNDOS',  '20',   'Segundos en pantalla de resultado antes de volver a standby',        'ENTERO'),
    ('REGISTRO_TIMEOUT_SEGUNDOS', '60',   'Segundos de inactividad en la pantalla de registro de DNI',          'ENTERO'),
    ('CICLO_HABILITADO',          '1',    'Si esta en 1, una tajada premiada no repite hasta cerrar el ciclo',  'BOOL'),
    ('DNI_LONGITUD',              '8',    'Digitos exactos que debe tener el DNI para aceptar el registro',     'ENTERO')
) AS origen (Clave, Valor, Descripcion, TipoDato)
    ON destino.Clave = origen.Clave
WHEN NOT MATCHED THEN
    INSERT (Clave, Valor, Descripcion, TipoDato)
    VALUES (origen.Clave, origen.Valor, origen.Descripcion, origen.TipoDato)
WHEN MATCHED THEN
    UPDATE SET Descripcion = origen.Descripcion, TipoDato = origen.TipoDato;
GO

/* --- Sala y pedestal por defecto ---------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Sala)
    INSERT dbo.Sala (Nombre) VALUES ('Sala principal');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Pantalla WHERE Codigo = 'PEDESTAL-01')
    INSERT dbo.Pantalla (Codigo, Nombre, SalaId)
    VALUES ('PEDESTAL-01', 'Pedestal 1', (SELECT MIN(SalaId) FROM dbo.Sala));
GO

/* ---------------------------------------------------------------------------
   Usuario administrador inicial
     Usuario: admin      Clave: Admin.2026

   El hash se calcula con el mismo algoritmo de la aplicacion (PBKDF2-SHA256),
   asi que no tiene sentido escribirlo a mano aqui: se deja un marcador y el
   servicio lo reemplaza por el hash real en el primer arranque.

   Nace con DebeCambiarPassword = 1: el sistema obliga a cambiar la clave al
   primer ingreso.
   --------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WHERE NombreUsuario = 'admin')
    INSERT dbo.Usuario (NombreUsuario, NombreCompleto, PasswordHash, Rol, DebeCambiarPassword)
    VALUES ('admin', 'Administrador', 'SEMBRAR_EN_ARRANQUE', 'ADMIN', 1);
GO
