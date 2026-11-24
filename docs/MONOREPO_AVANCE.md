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
- [x] `Pruebas` (dotnet test) pasa en GitHub Actions con la nueva estructura.
- [x] `eyesofterandroid` (rama `claude/gracious-pasteur-b403lw`): comentarios y docs apuntan a `eyesofterweb/portal` y `kmp/`.
- [x] Fase 2 (código listo, falta verlo en Railway):
  - `portal/Dockerfile` tiene la etapa `appweb` (eclipse-temurin:21) que compila
    `:app:webApp:wasmJsBrowserDistribution` y copia `productionExecutable/` a `wwwroot/app-web/`.
  - `CabecerasSeguridad`: CSP propia para `/app-web` (`wasm-unsafe-eval`, API Ktor, OSM, geolocalización); pruebas agregadas.
  - `Program.cs`: MIME `.wasm`/`.mjs`; `/app-web` → `/app-web/` → `index.html`.
  - Workflow `app-web.yml` compila la app web en cada cambio de `kmp/`.
  - `App web` pasa en Actions (~4½ min). La página "Descarga la app" tiene el botón de iPhone → `/app-web/`.
- [x] iOS (inicio): `kmp/app/iosApp` ya existía (Xcode + SwiftUI que carga `MainViewController`).
  - `IosLocationProvider` (CoreLocation) en `iosMain`, pasado a `App()`; permiso en `Info.plist`.
  - Workflow `ios.yml`: `xcodebuild` para simulador sin firmar en `macos-latest`.

## Pendiente — fase 1 (monorepo)

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

- Revisar que el workflow `iOS` pase.
- [x] Push en iPhone con FCM (el servidor no cambia): `PushIos` (iosMain) + `iOSApp.swift` (AppDelegate con Firebase),
  paquete `firebase-ios-sdk` en el proyecto Xcode, entitlement `aps-environment`, `UIBackgroundModes: remote-notification`.
  Sin `GoogleService-Info.plist` la app funciona sin push. Para activarlas (lo hace la persona dueña de la cuenta):
  1. En Firebase (mismo proyecto que Android): agregar app iOS con el bundle id definitivo y bajar `GoogleService-Info.plist`.
  2. Guardar su contenido como secreto `GOOGLE_SERVICE_INFO_PLIST` en GitHub.
  3. En Apple Developer crear una llave APNs (.p8) y subirla en Firebase → Configuración → Cloud Messaging → app iOS.
  4. Al publicar en TestFlight/App Store cambiar `aps-environment` a `production` (o dejar que la firma de distribución lo haga).
- Pendiente en `iosMain`: `Telefono` (cámara/vibración), mapa nativo (`DirectorioNativo`, opcional:
  sin él usa el mapa ilustrado). Todos son parámetros opcionales de `App()`: la app ya funciona sin ellos.
- `Config.xcconfig`: cambiar `PRODUCT_BUNDLE_IDENTIFIER` (hoy `com.example...`) y `TEAM_ID` al tener la cuenta de Apple.
- Pantalla Actualizar: en iOS mandar a App Store/TestFlight.
- Workflow `publicar-ios.yml` en `macos-latest` → TestFlight. Requiere cuenta Apple Developer (la de la usuaria/usuario).
