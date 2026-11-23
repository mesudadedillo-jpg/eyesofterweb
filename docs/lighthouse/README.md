# Validación con Lighthouse (escritorio y móvil)

Medido el 23 de noviembre de 2026 con **Lighthouse 12.8.2** (Chromium sin interfaz), portal compilado en
`Release` y corriendo en modo `Production` por HTTPS, con PostgreSQL 16 local.

- **Móvil:** configuración por defecto de Lighthouse (teléfono Moto G Power, 4G lenta simulada, CPU 4× más lenta).
- **Escritorio:** `--preset=desktop`.

Reportes completos de la página de inicio de sesión (donde está el botón de Google):
[`login-movil.html`](login-movil.html) · [`login-escritorio.html`](login-escritorio.html).
Capturas: [`login-movil.png`](login-movil.png) · [`login-escritorio.png`](login-escritorio.png).

## Resultado (rendimiento · accesibilidad · buenas prácticas · SEO)

| Página | Móvil antes | Móvil después | Escritorio antes | Escritorio después |
|---|---|---|---|---|
| `/Cuenta/Login` | 76 · 100 · 96 · 90 | **96 · 100 · 100 · 100** | 99 · 100 · 96 · 90 | **100 · 100 · 100 · 100** |
| `/` (inicio) | 78 · 100 · 96 · 90 | **97 · 100 · 100 · 100** | 99 · 100 · 96 · 90 | **100 · 100 · 100 · 100** |
| `/App` | 82 · 100 · 96 · 90 | **97 · 100 · 100 · 100** | 99 · 100 · 96 · 91 | **100 · 100 · 100 · 100** |
| `/Privacidad/Seguridad` | 77 · 100 · 96 · 90 | **96 · 100 · 100 · 100** | 99 · 100 · 96 · 90 | **100 · 100 · 100 · 100** |
| `/Cuenta/Olvide` | 77 · 96 · 96 · 90 | **95 · 100 · 100 · 100** | 99 · 100 · 96 · 90 | **100 · 100 · 100 · 100** |
| `/Soporte/Ayuda` | 78 · 100 · 96 · 90 | **95 · 100 · 100 · 100** | 99 · 100 · 96 · 90 | **100 · 100 · 100 · 100** |

Login en móvil: primer contenido visible (FCP) y elemento más grande (LCP) de **4.2 s → 2.3 s**; bloqueo total 0 ms; sin
saltos de diseño (CLS 0).

## Qué se corrigió

| Hallazgo de Lighthouse | Corrección |
|---|---|
| CSS y JS sin comprimir (≈ 407 KiB de más) | `UseResponseCompression` con Brotli/gzip **solo para archivos estáticos** (CSS, JS, SVG, íconos). El HTML no se comprime: lleva el token antifalsificación y comprimirlo por HTTPS lo expondría a BREACH (`Seguridad/ArchivosEstaticos.cs`). |
| Archivos estáticos sin caché | `Cache-Control`: 1 año e `immutable` para los que llevan `?v=` (`asp-append-version`), 7 días para `wwwroot/lib` y el ícono, 1 hora el resto. Las páginas con sesión siguen con `no-store`. |
| Error en consola: `/favicon.ico` 404 | `wwwroot/favicon.svg` (con los colores del portal) y `wwwroot/favicon.ico`. |
| Falta la meta descripción (SEO) | `<meta name="description">` en `_Layout.cshtml` (cada vista puede cambiarla con `ViewData["Descripcion"]`) y `theme-color`. |
| Enlaces difíciles de tocar en móvil (`/Cuenta/Olvide`) | Clase `.enlace-tactil` (mínimo 24 px de alto, WCAG 2.2 · 2.5.8). |

Lo que queda como oportunidad (no baja la calificación de 95): Bootstrap completo se carga aunque cada página
usa una parte; recortarlo requeriría compilar Bootstrap a la medida.

## Repetir la medición

```bash
# Portal local por HTTPS (en otra terminal): cd EyesofterWeb && dotnet run --launch-profile https
scripts/lighthouse.sh https://localhost:7080
# o contra Railway:
scripts/lighthouse.sh https://<dominio>.up.railway.app
```

El script audita las 6 páginas públicas en móvil y escritorio, deja los reportes en `lighthouse-reportes/` y
termina con error si alguna categoría queda abajo de 90 (`MINIMO=95 scripts/lighthouse.sh …` para exigir más).
