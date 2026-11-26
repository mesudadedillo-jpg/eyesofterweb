# EYESOFTER Suite — Portal web

Portal de gestión del programa de tamizaje visual de la Escuela Primaria-Secundaria METANOIA
(Tulancingo, Hgo.), para Dirección, Enfermería, tutores y especialistas de clínicas asociadas.

**Es el dueño de la base de datos y de las cuentas**: el servidor de la app móvil
([`eyesofterandroid`](https://github.com/mesudadedillo-jpg/eyesofterandroid)) usa la misma base
PostgreSQL y las mismas cuentas.

## Monorepo: portal + app multiplataforma

Este repositorio tiene los dos frentes de EYESOFTER Suite:

| Carpeta | Qué es | Cómo se publica |
|---|---|---|
| `EyesofterWeb/` (+ `EyesofterWeb.Tests/`) | Portal web, ASP.NET Core 10 | Railway (`Dockerfile`, `railway.json`) |
| `EyesofterApp/` | App Kotlin Multiplatform / Compose: **Android, escritorio (JVM), web (Wasm/JS) e iOS** | APK en *Releases* de este repo (`.github/workflows/publicar-apk.yml`) |

La interfaz de la app está **una sola vez** en `EyesofterApp/app/shared` (Compose Multiplatform): un cambio
ahí sale igual en Android, en la versión de escritorio y en la web de la app. Ver
[`EyesofterApp/README.md`](EyesofterApp/README.md). Antes la app vivía en el repo `eyesofterandroidfront`
(su historial se conservó aquí con `git subtree`).

| Documento | Contenido |
|---|---|
| [`docs/MANUAL_TECNICO.md`](docs/MANUAL_TECNICO.md) | Arquitectura, modelo de datos, instalación y despliegue (C-RS-01) |
| [`docs/MANUAL_USUARIO.md`](docs/MANUAL_USUARIO.md) | Manual para Enfermería y Dirección (W-RS-01) |
| [`docs/CAPACITACION.md`](docs/CAPACITACION.md) | Temario de capacitación y lista previa al piloto (W-RS-02, C-RS-08) |
| [`docs/RESPALDO.md`](docs/RESPALDO.md) | Respaldo diario y recuperación de la base (C-RC-13, C-RS-05) |
| [`docs/TRAZABILIDAD.md`](docs/TRAZABILIDAD.md) | Los 119 requisitos y dónde se cumple cada uno |
| `eyesofterandroid/docs/API.md` | API de la app y del escritorio (C-RS-02) |
| [`EyesofterApp/README.md`](EyesofterApp/README.md) | App multiplataforma: ejecutar, probar y publicar el APK |

## Tecnologías

.NET 10 · ASP.NET Core MVC · ASP.NET Core Identity (PBKDF2) · EF Core 10 + Npgsql (PostgreSQL) ·
QuestPDF · ClosedXML · QRCoder · html5-qrcode · Bootstrap 5.3 (local, sin CDN)

## Ejecutar en tu computadora

Requisitos: SDK de .NET 10 y PostgreSQL 16 o más nuevo.

```bash
createdb eyesofter
```

```bash
cd EyesofterWeb
dotnet run
```

Abre `http://localhost:5080`. En desarrollo se aplican solas las migraciones y se crean datos de ejemplo.
La cadena de conexión local está en `appsettings.json` (usuario y contraseña `postgres`).

| Rol | Correo | Contraseña |
|---|---|---|
| Dirección (administrador global: todas las escuelas) | admin@eyesofter.mx | Eyesofter123! |
| Enfermería (METANOIA) | enfermeria@eyesofter.mx | Eyesofter123! |
| Enfermería (Secundaria Técnica 5, escuela demo) | enfermeria2@eyesofter.mx | Eyesofter123! |
| Tutor | tutor@eyesofter.mx | Eyesofter123! |
| Alumno | alumno@eyesofter.mx | Eyesofter123! |
| Especialista (clínica asociada) | especialista@eyesofter.mx | Eyesofter123! |

En producción **no** se crean datos de ejemplo: la primera cuenta de Dirección sale de `ADMIN_CORREO`
y `ADMIN_PASSWORD`.

## Varias escuelas (C-RE-01)

Una sola instalación atiende a varias escuelas y **cada una solo ve a sus alumnos**:

- Dirección y Enfermería ven únicamente las escuelas que tienen asignadas (Usuarios → casillas *Escuelas*).
  El filtro vive en un solo lugar (`Seguridad/AlcanceEscuela.cs`, aplicado como filtro global de EF Core),
  así que una pantalla nueva no puede mostrar por error alumnos de otra escuela.
- El **administrador global** (marca en una cuenta de Dirección; la primera es `ADMIN_GLOBAL_CORREO` o, si no
  se define, `ADMIN_CORREO`) ve todas, da de alta escuelas (Más → Escuelas) y atiende los reportes de fallas.
  Es para el equipo que administra EYESOFTER, no para el director de una escuela.
- **Sumar una escuela:** el administrador global la crea en *Escuelas* y le da de alta su Dirección en
  *Usuarios* (marcando esa escuela). Desde ahí, esa Dirección crea a su Enfermería, alumnos y tutores.
- **Convenios** (Más → Convenios): cada escuela registra el beneficio que una clínica da a sus alumnos.
  Las familias lo ven en el directorio y en el pase; el especialista, al validar el QR. La clínica no
  recibe ningún dato por tener convenio: solo ve el caso cuando la familia la elige y llega con su pase.
- La matrícula es única **dentro de cada escuela**.

## Iniciar sesión con Google

En la pantalla de inicio de sesión aparece el botón **Iniciar sesión con Google** (solo si están configuradas
`GOOGLE_CLIENT_ID` y `GOOGLE_CLIENT_SECRET`). Google solo dice quién es la persona: **no hay registro público**,
las cuentas las sigue creando la Dirección.

- **Primera vez:** se vincula con la cuenta de EYESOFTER que tiene **el mismo correo**, solo si Google confirma que
  el correo es de esa persona (`email_verified`). Si el correo no tiene cuenta, se le pide que la solicite a la escuela.
- **Después:** se reconoce por el identificador de la cuenta de Google, aunque cambie su correo de Gmail.
- **Otro correo:** con la sesión iniciada, *menú de la cuenta → Iniciar sesión con Google → Vincular* (por ejemplo,
  la escuela registró un correo del trabajo y el tutor usa Gmail). Ahí también se puede **quitar**.
- **Aviso por correo** (Resend) cada vez que se vincula o se quita Google, con el enlace para quitarlo si no fue la persona.
- Se siguen aplicando: cuentas desactivadas, bloqueo de 15 min por intentos y la **verificación en dos pasos** de
  Dirección y Enfermería (Google no la sustituye). Solo se piden los permisos `openid profile email`; Google siempre
  pregunta qué cuenta usar (computadoras compartidas). La contraseña sigue funcionando en el portal y en la app.
- Bitácora: `GOOGLE_VINCULADA`, `GOOGLE_DESVINCULADA`, `LOGIN_GOOGLE_RECHAZADO` y `LOGIN` con detalle "Con Google".
- Las reglas están en `Servicios/AccesoGoogle.cs`; el flujo, en `CuentaController` (`LoginGoogle`, `RetornoGoogle`,
  `Google`, `VincularGoogle`, `DesvincularGoogle`).

**Configurar (una vez):**

1. [Google Cloud Console](https://console.cloud.google.com) → crea un proyecto → *APIs y servicios →
   Pantalla de consentimiento de OAuth*: tipo **Externo**, nombre "EYESOFTER", correo de soporte, y los permisos
   `openid`, `.../auth/userinfo.email` y `.../auth/userinfo.profile`. Publica la app (estado *En producción*);
   en *Prueba* solo entran los correos que agregues como usuarios de prueba.
2. *Credenciales → Crear credenciales → ID de cliente de OAuth* → **Aplicación web**.
   - Orígenes autorizados: `https://eyesofterweb-production.up.railway.app` (y `https://localhost:7080` para probar en tu PC).
   - **URI de redireccionamiento autorizados:** `https://eyesofterweb-production.up.railway.app/signin-google`
     (y `https://localhost:7080/signin-google`).
3. En Railway, variables del portal: `GOOGLE_CLIENT_ID` y `GOOGLE_CLIENT_SECRET` (y que `Eyesofter__UrlWeb` sea
   `https://eyesofterweb-production.up.railway.app`, para que el enlace de los correos de aviso sea el correcto). En tu PC, sin ponerlas en el repositorio:
   `dotnet user-secrets set GOOGLE_CLIENT_ID "..."` y `dotnet user-secrets set GOOGLE_CLIENT_SECRET "..."` (en `EyesofterWeb/`).

Si Google responde `redirect_uri_mismatch`, la URI del paso 2 no coincide exactamente (https, dominio y `/signin-google`).

## Seguridad

- **Verificación en dos pasos (TOTP)** con cualquier app autenticadora. **Obligatoria para Dirección y
  Enfermería**: al entrar por primera vez se les pide activarla (código QR + 10 códigos de respaldo) y no
  ven ningún dato antes. El servidor de la app pide el mismo código. Si alguien pierde su teléfono,
  Dirección usa *Usuarios → Restablecer 2 pasos*. `Eyesofter__Exigir2FAPersonal=false` la vuelve opcional.
  En el modo demo también se pide: escanea el QR con tu app autenticadora.
- Las páginas con sesión se envían con `Cache-Control: no-store` (computadoras compartidas).
- El login no revela si un correo existe o si la cuenta está desactivada (solo a quien sabe la contraseña).
- Las exportaciones de casos (Excel/PDF) quedan en la bitácora.
- Página pública **Tu información está segura** (`/Privacidad/Seguridad`) y `/.well-known/security.txt`
  para reportar vulnerabilidades.

## Desplegar en Railway

El repositorio trae `Dockerfile` y `railway.json` (healthcheck en `/salud`).

1. En el proyecto de Railway que ya tiene PostgreSQL: *+ New → GitHub Repo → eyesofterweb*.
2. *Variables*:

| Variable | Valor |
|---|---|
| `DATABASE_URL` | `${{Postgres.DATABASE_URL}}` |
| `EYESOFTER_CLAVE_DATOS` | **La misma** que tiene el servidor de la app |
| `ADMIN_CORREO` / `ADMIN_PASSWORD` | Cuenta inicial de Dirección (mín. 8 caracteres, con mayúscula, minúscula y número) |
| `Eyesofter__Exigir2FAPersonal` | (Opcional) `false` para que la verificación en dos pasos del personal sea opcional. No recomendado |
| `ADMIN_GLOBAL_CORREO` | (Opcional) Correo de la cuenta de Dirección que será administrador global si todavía no hay ninguno. Si no se define, se usa `ADMIN_CORREO` |
| `Eyesofter__UrlWeb` | URL pública de este portal (va en los correos) |
| `Eyesofter__UrlApi` | URL pública del servidor de la app |
| `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` | (Opcional) Botón **Iniciar sesión con Google** (ver "Iniciar sesión con Google"). Sin ellas el botón no aparece |
| `BREVO_API_KEY` / `CORREO_REMITENTE` | Correo: código para recuperar contraseñas, enlace para crear la contraseña de cuentas nuevas y avisos de Google. Con [Brevo](https://www.brevo.com) (gratis, 300 correos al día) **no se necesita dominio propio**: ver "Correo sin dominio propio (Brevo)". Las mismas en el servidor de la app |
| `RESEND_API_KEY` | Alternativa a Brevo con [Resend](https://resend.com) (3 000 al mes). Para enviar a cualquiera, el remitente debe ser de un dominio verificado en Resend; con `onboarding@resend.dev` solo llega al correo de tu cuenta de Resend. Si están las dos llaves, se usa Brevo |
| `TWILIO_ACCOUNT_SID` / `TWILIO_AUTH_TOKEN` / `TWILIO_NUMERO` | (Opcional) Código por SMS al celular del tutor. Twilio cobra (≈ US$0.05 por SMS a México; la cuenta de prueba solo envía a números verificados). Sin ellas, a quien escribe su celular el código le llega al correo. Las mismas en el servidor de la app |
| `SMS_PREFIJO_PAIS` | (Opcional) Lada para celulares de 10 dígitos. Por defecto `+52` |
| `Eyesofter__UrlApk` | (Opcional) Dónde se descarga el instalador de Android. Por defecto, el último APK publicado en GitHub Releases de este repositorio (`eyesofterweb`, flujo *Publicar APK*) (página pública `/App`) |
| `CUENTAS_PRUEBA_PASSWORD` | (Opcional) Crea y mantiene una cuenta de prueba por rol en una escuela ficticia aparte (ver abajo). Pon esta variable a mano en el panel; no la publiques |
| `CUENTAS_PRUEBA_CORREO` | (Opcional) Correo base de las cuentas de prueba. Si no se define, se usa `ADMIN_CORREO` |

3. *Settings → Networking → Generate Domain*. Pon esa URL en `URL_PORTAL` del servidor de la app.
4. Al arrancar aplica las migraciones y crea la cuenta de Dirección. Revisa `https://<dominio>/salud`.

### Cuentas de prueba (una por rol)

Con `CUENTAS_PRUEBA_PASSWORD`, en cada arranque el portal asegura una **Escuela de prueba (datos ficticios)**
con un tutor, dos alumnos, dos casos (rojo y amarillo), una óptica ficticia con convenio y un pase vigente,
y estas cuentas, todas con esa contraseña (con el correo base `micorreo@gmail.com`):

| Rol | Cuenta |
|---|---|
| Dirección (solo la escuela de prueba) | `micorreo+direccion@gmail.com` |
| Enfermería (solo la escuela de prueba) | `micorreo+enfermeria@gmail.com` |
| Tutor | `micorreo+tutor@gmail.com` |
| Alumno | `micorreo+alumno@gmail.com` |
| Especialista de la óptica de prueba | `micorreo+especialista@gmail.com` |

Gmail y Outlook entregan los correos con `+algo` al buzón original, así que la recuperación de contraseña
llega a quien las configuró. Dirección de prueba también activa la verificación en dos pasos (para Enfermería y el resto es opcional).
Si se borra algo, se vuelve a crear al reiniciar; el pase de prueba se renueva al vencer; si cambias
`CUENTAS_PRUEBA_PASSWORD`, las cuentas toman la nueva contraseña. La óptica de prueba aparece en el
directorio de clínicas con la leyenda "(ficticia)".

Si falta `EYESOFTER_CLAVE_DATOS`, el portal se detiene a propósito con un mensaje claro en los logs:
sin esa llave no puede proteger los datos de los alumnos.

## Correo sin dominio propio (Brevo)

1. Crea una cuenta gratis en [brevo.com](https://www.brevo.com). Conviene un Gmail del proyecto (p. ej. `eyesofter.avisos@gmail.com`).
2. *Senders, Domains & Dedicated IPs → Senders → Add a sender*: nombre `EYESOFTER` y ese Gmail. Brevo manda un
   código a ese Gmail para verificarlo.
3. *SMTP & API → API Keys → Generate a new API key* (empieza con `xkeysib-`).
4. En Railway, **en el portal y en el servidor de la app**: `BREVO_API_KEY` = la llave y
   `CORREO_REMITENTE` = `EYESOFTER <eyesofter.avisos@gmail.com>`. Puedes quitar `RESEND_API_KEY`.

Sin dominio propio, Gmail y Outlook a veces mandan estos correos a **Spam**: pide a las familias que revisen esa
carpeta y marquen "No es spam". Con un dominio verificado (en Brevo o Resend) llegan mejor.

## Pruebas unitarias

```bash
dotnet test
```

`EyesofterWeb.Tests/` (xUnit, sin base de datos ni internet: Identity sobre EF Core en memoria y correo/HTTP falsos):

| Archivo | Qué prueba |
|---|---|
| `AccesoGoogleTests.cs` | Vincular por correo verificado, reconocer por el id de Google, correo no verificado, sin cuenta, cuenta desactivada, una sola cuenta de Google por cuenta, vincular otro correo con sesión, quitar Google, avisos por correo y bitácora |
| `CorreoTests.cs`, `CorreoBrevoTests.cs` | Envío por Resend y por Brevo: URL, llave, remitente, destinatario, asunto y HTML; elección del proveedor; errores y sin red; sin configurar no envía |
| `CuentaControllerTests.cs` | El botón de Google aparece solo si está configurado; aviso al cancelar; sin configurar regresa al login |
| `CabecerasSeguridadTests.cs` | La CSP permite el formulario hacia `accounts.google.com` y sigue sin scripts de terceros ni iframes |
| `ArchivosEstaticosTests.cs` | Caché de archivos estáticos y que el HTML no se comprima |
| `RecuperacionPasswordTests.cs`, `LoginViewModelTests.cs` | Correo o celular, código por correo/SMS y validación del formulario de login |

GitHub Actions las corre en cada push (`.github/workflows/pruebas.yml`).

## Lighthouse (escritorio y móvil)

Las 6 páginas públicas obtienen **100 en accesibilidad, buenas prácticas y SEO** en móvil y escritorio;
rendimiento **95–97 en móvil** y **100 en escritorio**. Detalle, antes/después y reportes en
[`docs/lighthouse/`](docs/lighthouse/README.md). Para repetirlo: `scripts/lighthouse.sh https://eyesofterweb-production.up.railway.app` (o `https://localhost:7080`).

## Estructura

```
EyesofterWeb/
├─ Controllers/   Casos, Comprobantes, Pases, MisHijos, Clínicas, Alumnos, Tutores, Usuarios, Campañas, Avisos, Auditoría, Soporte, Privacidad, Cuenta
├─ Data/          ApplicationDbContext, conexión (DATABASE_URL), siembra inicial
├─ Migrations/    Esquema de la base (también lo usa el servidor de la app)
├─ Models/        Entidades y enums
├─ Servicios/     Expedientes (reglas del caso), Notificador, Indicadores, Documentos (PDF/Excel/QR), Auditoría, Correo, AccesoGoogle
├─ Seguridad/     Cifrado AES-256-GCM, cabeceras, consentimiento, caché de archivos estáticos
└─ Views/ wwwroot/
```
