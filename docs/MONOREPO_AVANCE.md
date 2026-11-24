# Fusión en monorepo — avance y pendientes

Objetivo: `eyesofterweb` contiene el portal (.NET) y la app KMP (antes `eyesofterandroidfront`).
Railway: 2 servicios (portal de este repo + API Ktor de `eyesofterandroid`) y PostgreSQL.
Después: servir la versión web de la app desde el portal y agregar iOS.

## Hecho (rama `claude/gracious-pasteur-b403lw`)

- [x] Portal movido a `portal/` (EyesofterWeb, Tests, .sln, Dockerfile, scripts).
- [x] App importada con `git subtree add --prefix=kmp` desde `eyesofterandroidfront` `main` (a6546cd), con historial.
- [x] `railway.json` y `.dockerignore` en la raíz; `dockerfilePath: portal/Dockerfile`, que copia desde `portal/EyesofterWeb/` (se construye desde la raíz).
- [x] `pruebas.yml` → `dotnet test portal/EyesofterWeb.sln`.
- [x] `publicar-apk.yml` movido a `.github/workflows/`, con `working-directory: kmp`, `paths: kmp/**`
      y `versionCode = run_number + 1000` (el número de ejecución se reinicia en el repo nuevo; así nunca baja).
- [x] URLs del APK/version.json → `eyesofterweb/releases` en `VersionApp.kt`, `ActualizarScreen.kt` y `OpcionesEyesofter.cs`.
- [x] README, MANUAL_TECNICO, TRAZABILIDAD, kmp/README y kmp/docs/API actualizados.

## Pendiente — fase 1 (monorepo)

1. Comprobar en GitHub Actions que `Pruebas` pasa (no había Docker ni .NET en la sesión).
2. **Railway (servicio del portal):** dejar *Root Directory* vacío y confirmar que usa `railway.json` de la raíz. Verificar `/salud` después del deploy.
3. **Secretos de GitHub** en `eyesofterweb` (copiarlos de `eyesofterandroidfront`): `EYESOFTER_KEYSTORE_BASE64`,
   `EYESOFTER_KEYSTORE_PASSWORD`, `EYESOFTER_KEY_ALIAS`, `GOOGLE_SERVICES_JSON`. Misma llave de firma, o Android no actualizará encima.
4. Tras el merge a `main`: confirmar que se publica el release `app-v…` con `version.json` en este repo.
5. **Puente en el repo viejo** (`eyesofterandroidfront`): las apps instaladas leen
   `eyesofterandroidfront/releases/latest/download/version.json`. Publicar ahí una última release cuyo
   `version.json` apunte al APK nuevo (`versionCode` ≥ 1001), y luego **archivar** el repo (no borrarlo).
6. Variable `Eyesofter__UrlApk` en Railway: si existe con la URL vieja, quitarla o actualizarla.
7. `eyesofterandroid`: actualizar comentarios/documentos (`CifradoDatos.kt:13` → `eyesofterweb/portal/EyesofterWeb/Seguridad/CifradoDatos.cs`,
   `BaseDeDatos.kt:20`, README, docs/API.md).
8. Escritorio Java (`Eyesofter`): revisar si su botón de descarga apunta a `eyesofterandroidfront` y cambiarlo.

## Fase 2 — versión web de la app servida por el portal

- Agregar a `portal/Dockerfile` una etapa con JDK que compile `kmp` (`:app:webApp:wasmJsBrowserDistribution`)
  y copie la salida a `wwwroot/app/`. Quitar `kmp/**` de `.dockerignore` lo necesario.
- Revisar en el portal los tipos MIME (`.wasm` → `application/wasm`) y la política CSP para `/app`.

## Fase 3 — iOS

- Crear `kmp/iosApp/` (proyecto Xcode que carga el framework `shared`; ya hay objetivos `iosArm64`/`iosSimulatorArm64`).
- `iosMain`: ubicación (equivalente de `AndroidLocationProvider`), mapa, abrir/compartir PDF, almacenamiento, push (FCM + llave APNs).
- Pantalla Actualizar: en iOS mandar a App Store/TestFlight.
- Workflow `publicar-ios.yml` en `macos-latest` → TestFlight. Requiere cuenta Apple Developer (la de la usuaria/usuario).
