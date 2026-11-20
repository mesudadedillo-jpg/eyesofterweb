# EYESOFTER Suite — Portal web

Portal de gestión del programa de tamizaje visual de la Escuela Primaria-Secundaria METANOIA
(Tulancingo, Hgo.), para Dirección, Enfermería, tutores y especialistas de clínicas asociadas.

**Es el dueño de la base de datos y de las cuentas**: el servidor de la app móvil
([`eyesofterandroid`](https://github.com/mesudadedillo-jpg/eyesofterandroid)) usa la misma base
PostgreSQL y las mismas cuentas.

| Documento | Contenido |
|---|---|
| [`docs/MANUAL_TECNICO.md`](docs/MANUAL_TECNICO.md) | Arquitectura, modelo de datos, instalación y despliegue (C-RS-01) |
| [`docs/MANUAL_USUARIO.md`](docs/MANUAL_USUARIO.md) | Manual para Enfermería y Dirección (W-RS-01) |
| [`docs/CAPACITACION.md`](docs/CAPACITACION.md) | Temario de capacitación y lista previa al piloto (W-RS-02, C-RS-08) |
| [`docs/RESPALDO.md`](docs/RESPALDO.md) | Respaldo diario y recuperación de la base (C-RC-13, C-RS-05) |
| [`docs/TRAZABILIDAD.md`](docs/TRAZABILIDAD.md) | Los 119 requisitos y dónde se cumple cada uno |
| `eyesofterandroid/docs/API.md` | API de la app y del escritorio (C-RS-02) |

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
| Dirección | admin@eyesofter.mx | Eyesofter123! |
| Enfermería | enfermeria@eyesofter.mx | Eyesofter123! |
| Tutor | tutor@eyesofter.mx | Eyesofter123! |
| Alumno | alumno@eyesofter.mx | Eyesofter123! |
| Especialista (clínica asociada) | especialista@eyesofter.mx | Eyesofter123! |

En producción **no** se crean datos de ejemplo: la primera cuenta de Dirección sale de `ADMIN_CORREO`
y `ADMIN_PASSWORD`.

## Desplegar en Railway

El repositorio trae `Dockerfile` y `railway.json` (healthcheck en `/salud`).

1. En el proyecto de Railway que ya tiene PostgreSQL: *+ New → GitHub Repo → eyesofterweb*.
2. *Variables*:

| Variable | Valor |
|---|---|
| `DATABASE_URL` | `${{Postgres.DATABASE_URL}}` |
| `EYESOFTER_CLAVE_DATOS` | **La misma** que tiene el servidor de la app |
| `ADMIN_CORREO` / `ADMIN_PASSWORD` | Cuenta inicial de Dirección (mín. 8 caracteres, con mayúscula, minúscula y número) |
| `Eyesofter__UrlWeb` | URL pública de este portal (va en los correos) |
| `Eyesofter__UrlApi` | URL pública del servidor de la app |
| `RESEND_API_KEY` / `CORREO_REMITENTE` | Correo para recuperar contraseñas (opcional al principio) |

3. *Settings → Networking → Generate Domain*. Pon esa URL en `URL_PORTAL` del servidor de la app.
4. Al arrancar aplica las migraciones y crea la cuenta de Dirección. Revisa `https://<dominio>/salud`.

Si falta `EYESOFTER_CLAVE_DATOS`, el portal se detiene a propósito con un mensaje claro en los logs:
sin esa llave no puede proteger los datos de los alumnos.

## Estructura

```
EyesofterWeb/
├─ Controllers/   Casos, Comprobantes, Pases, MisHijos, Clínicas, Alumnos, Tutores, Usuarios, Campañas, Avisos, Auditoría, Soporte, Privacidad, Cuenta
├─ Data/          ApplicationDbContext, conexión (DATABASE_URL), siembra inicial
├─ Migrations/    Esquema de la base (también lo usa el servidor de la app)
├─ Models/        Entidades y enums
├─ Servicios/     Expedientes (reglas del caso), Notificador, Indicadores, Documentos (PDF/Excel/QR), Auditoría, Correo
├─ Seguridad/     Cifrado AES-256-GCM, cabeceras, consentimiento
└─ Views/ wwwroot/
```
