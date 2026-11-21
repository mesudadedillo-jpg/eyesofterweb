# Manual técnico — EYESOFTER Suite (C-RS-01)

## 1. Arquitectura

```
                          ┌──────────────────────────────── Railway (un proyecto) ───────────────────────────────┐
 Navegador (Dirección,    │  eyesofterweb  (ASP.NET Core 10, MVC + Identity + EF Core)                           │
 Enfermería, tutores,  ───┼─►  · expedientes, tablero, clínicas, usuarios, pases, auditoría                      │
 especialistas)   HTTPS   │    · crea el esquema (migraciones) y las cuentas                       ┌─────────────┐│
                          │                                                                        │ PostgreSQL  ││
 App móvil (KMP) ─────────┼─►  eyesofterandroid  (Ktor 3, JDBC + HikariCP)            ───────────► │ (una sola   ││
 Sistema de escritorio ───┼─►  · API de la app y del escritorio (docs/API.md)                      │  base)      ││
           HTTPS/JSON     │    · despachador: entrega push (Firebase) y correo (Resend)            └─────────────┘│
                          └──────────────────────────────────────────────────────────────────────────────────────┘
```

| Pieza | Repositorio | Tecnología |
|---|---|---|
| Portal web | `eyesofterweb` | .NET 10, ASP.NET Core MVC, Identity, EF Core 10 + Npgsql, QuestPDF, ClosedXML, QRCoder, html5-qrcode |
| Servidor de la app | `eyesofterandroid` | Kotlin 2.4, Ktor 3.6, JDBC + HikariCP, ZXing, Firebase HTTP v1, Resend |
| App móvil | `eyesofterandroidfront` | Kotlin Multiplatform, Compose Multiplatform 1.12, MapLibre 11, multiplatform-settings |
| Base de datos | Railway | PostgreSQL |

**Decisiones clave**

- **Una sola base**: el portal es dueño del esquema; el servidor Ktor no crea tablas. Así un
  cambio de estado se ve en la web y en la app al instante (C-RC-02).
- **Cuentas únicas** con ASP.NET Identity; el servidor Ktor verifica el mismo hash PBKDF2 y usa el
  mismo bloqueo (`access_failed_count`, `lockout_end`).
- **Cifrado de campo** AES-256-GCM con la llave `EYESOFTER_CLAVE_DATOS`, compartida por las dos
  aplicaciones (C-RC-04). Formato: texto `"v1:" + Base64(nonce‖cifrado‖tag)`; archivos `"E1"` + lo mismo.
- **Bandeja de salida**: toda notificación es una fila de `notificaciones` con
  `pendiente_envio = true`; el despachador de Ktor la entrega por push y, si falla, por correo.

## 2. Modelo de datos

Nombres en `snake_case` (EFCore.NamingConventions). Las tablas de Identity son `asp_net_*`.

| Tabla | Qué guarda | Campos cifrados |
|---|---|---|
| `planteles`, `campanas` | Escuelas y campañas de tamizaje (ciclo escolar, fechas, escuela) | — |
| `usuario_planteles` | Escuelas de cada cuenta de Dirección o Enfermería (C-RE-01) | — |
| `convenios` | Beneficio de una clínica para los alumnos de una escuela (único por escuela y clínica) | — |
| `tutores`, `alumnos` | Familias (con la escuela que las registró) y alumnos (matrícula única **por escuela**, grado, grupo, nivel) | — |
| `asp_net_users` (+ roles) | Cuentas: `tutor_id`, `alumno_id`, `clinica_id`, `activo`, `administrador_global` | — |
| `vinculaciones_escritorio` | Solicitudes para vincular un equipo de escritorio (huella del secreto, código, 10 min) | — |
| `clinicas` | Directorio: asociada, activa, coordenadas, horario | — |
| `tamizajes` | Resultado recibido (folio único, semáforo, origen) | agudezas, observaciones |
| `casos` | Expediente vivo (folio `EYS-AAAA-NNNN`, estado, destino) | agudezas, observaciones |
| `eventos_caso` | Línea de tiempo del caso | descripción |
| `pases` | Pase médico (código único, vence, un solo uso) | — |
| `atenciones` | Atención registrada (QR o manual) | valoración, indicaciones |
| `comprobantes` | Receta o comprobante de lentes | archivo |
| `citas` | Citas solicitadas | — |
| `notificaciones`, `entregas_notificacion` | Bandeja compartida e historial de entregas | — |
| `dispositivos` | Tokens de push por cuenta | — |
| `consentimientos` | Aceptación del aviso (versión, fecha, medio, IP) | — |
| `auditoria` | Bitácora de accesos y cambios (origen web/app/escritorio, escuela) | — |
| `reportes_falla` | Fallas y dudas de los usuarios | — |
| `data_protection_keys` | Llaves de las cookies del portal | — |

Relaciones principales: `tutores 1─N alumnos 1─N tamizajes`, `alumnos 1─N casos`,
`casos 1─N eventos_caso | pases | atenciones | comprobantes | citas`,
`clinicas 1─N pases | atenciones | citas`.

**Filtro por escuela:** `ApplicationDbContext` aplica un filtro global (`HasQueryFilter`) a alumnos, tutores,
campañas, convenios, tamizajes, casos, eventos, pases, atenciones, comprobantes y citas, según
`Seguridad/AlcanceEscuela.cs`. Para saltarlo a propósito (unicidad de matrícula, folios) se usa
`IgnoreQueryFilters()` con un comentario de por qué. El servidor Ktor aplica la misma regla (`Usuario.alcance`).

El diagrama completo sale de `EyesofterWeb/Migrations/*_Inicial.cs` y `*_Multiescuela.cs`. El servidor Ktor tiene una
copia del esquema para sus pruebas en `src/main/resources/esquema.sql`.

## 3. Instalación local

Requisitos: PostgreSQL 16+, .NET SDK 10, JDK 21 (Gradle descarga el resto), Android Studio para la app.

```bash
createdb eyesofter
```

**Portal web** (crea las tablas y los datos demo en desarrollo; contraseña demo `Eyesofter123!`):

```bash
cd eyesofterweb/EyesofterWeb
dotnet run
```

Abre `http://localhost:5080`. Las cuentas demo son `admin@`, `enfermeria@`, `tutor@`, `alumno@` y
`especialista@eyesofter.mx`.

**Servidor de la app:**

```bash
cd eyesofterandroid
DATABASE_URL="jdbc:postgresql://localhost:5432/eyesofter?user=postgres&password=postgres" ESCRITORIO_API_KEY=k123 URL_PORTAL=http://localhost:5080 ./gradlew run
```

**App:** abre `eyesofterandroidfront` en Android Studio. Para usar el servidor local, cambia
`BASE_URL` a `http://10.0.2.2:8080` en `PlatformConfig.kt`.

**Pruebas:**

```bash
cd eyesofterandroid && ./gradlew test
```

```bash
cd eyesofterandroidfront && ./gradlew :app:shared:jvmTest
```

## 4. Despliegue en Railway

Un proyecto con tres servicios. Cada repositorio trae su `Dockerfile` y su `railway.json`
(healthcheck en `/salud`, reinicio si falla).

1. **PostgreSQL**: *+ New → Database → PostgreSQL*.
2. **Portal web** (`eyesofterweb`): *+ New → GitHub Repo*. Variables:

| Variable | Valor |
|---|---|
| `DATABASE_URL` | `${{Postgres.DATABASE_URL}}` |
| `EYESOFTER_CLAVE_DATOS` | Llave AES de 32 bytes en Base64 (`openssl rand -base64 32`). **La misma en los dos servicios** |
| `ADMIN_CORREO` / `ADMIN_PASSWORD` | Primera cuenta de Dirección (se crea al primer arranque) |
| `Eyesofter__UrlWeb` | URL pública del portal (va en los correos) |
| `Eyesofter__UrlApi` | URL pública del servidor de la app |
| `RESEND_API_KEY` / `CORREO_REMITENTE` | Correo (recuperación de contraseña) |
| `SEMBRAR_DEMO` | `true` solo si quieres datos de ejemplo en producción |

   En *Settings → Networking* genera el dominio público.
3. **Servidor de la app** (`eyesofterandroid`): variables en su README (`DATABASE_URL`,
   `EYESOFTER_CLAVE_DATOS`, `JWT_SECRET`, `ESCRITORIO_API_KEY`, `URL_PORTAL`, `FIREBASE_SERVICE_ACCOUNT`,
   `RESEND_API_KEY`, `CORREO_REMITENTE`). También genera su dominio público: la app y el escritorio lo usan.
4. Revisa `GET /salud` en los dos servicios. En el servidor de la app debe salir `"db": true`.
5. Railway despliega la rama configurada en *Settings → Source* (normalmente `main`).

**Cambiar de plan (C-RE-04)**: no requiere cambios de código; solo subir recursos en Railway.

## 5. Bitácoras y diagnóstico (C-RS-03)

- Errores de ejecución: *Railway → servicio → Deployments → View logs*. El servidor de la app
  usa los nombres `errores`, `auditoria` y `Despachador`, para filtrarlos.
- Accesos denegados, bloqueos y acciones del personal: tabla `auditoria`, vista *Auditoría* del portal.
- Entregas de notificaciones: tabla `entregas_notificacion` y `/salud` → `pendientesEnvio`.

## 6. Seguridad

HTTPS con HSTS · cabeceras CSP, X-Frame-Options y nosniff · antiforgery en formularios · cookie de
15 min · bloqueo de cuenta · límite de 30 intentos/min por IP · consultas parametrizadas · archivos
validados por contenido · cifrado de campos · auditoría · aviso de privacidad versionado ·
contenedores sin root.

Las llaves rotan así: `JWT_SECRET` se cambia libremente (cierra las sesiones de la app).
`EYESOFTER_CLAVE_DATOS` **no se puede cambiar** sin volver a cifrar los datos: guárdala en un lugar seguro.

## 7. Respaldos

Ver [`RESPALDO.md`](RESPALDO.md).
