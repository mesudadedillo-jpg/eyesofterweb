# Seguridad — EYESOFTER Web (portal)

EYESOFTER trata **datos de salud de menores**. Resumen de las medidas del portal:

| Área | Medida |
|---|---|
| Acceso | Cuentas creadas por la escuela (sin registro público); contraseñas con hash PBKDF2 (Identity); bloqueo de 15 min tras 3 intentos |
| Verificación en dos pasos | TOTP **obligatorio** para Dirección y Enfermería; códigos de respaldo de un solo uso |
| Varias escuelas | Filtro global por escuela (`Seguridad/AlcanceEscuela.cs`): el personal solo ve su escuela; lo ajeno es 404 |
| Cifrado | HTTPS (HSTS); datos de salud y archivos cifrados en la base con AES-256-GCM |
| Sesión | Cookie HttpOnly/Secure/SameSite, cierre por inactividad (15 min), `Cache-Control: no-store` con sesión |
| Navegador | CSP sin scripts de terceros ni en línea, `frame-ancestors 'none'`, anti-CSRF en formularios |
| Bitácora | Accesos, cambios, exportaciones y accesos denegados quedan registrados por escuela |
| Respaldos | Diarios y cifrados, solo en un repositorio privado (`docs/RESPALDO.md`) |
| Familias | Página pública *Tu información está segura* (`/Privacidad/Seguridad`) y `/.well-known/security.txt` |

## Reportar una vulnerabilidad

Si encuentras una falla de seguridad, **no abras un issue público**. Usa
*Security → Report a vulnerability* de este repositorio (reporte privado de GitHub) o el formulario
*Reportar una falla* del portal. Responderemos lo antes posible.

## Datos y secretos

- **Todos los datos de este repositorio son ficticios** (nombres, CURP, teléfonos y correos de ejemplo).
  Ningún dato real de alumnos o familias se guarda en el código.
- **No hay secretos en el repositorio.** Llaves de cifrado, secretos de sesión, contraseñas y conexiones
  a la base viven solo en las variables de entorno de Railway.
- Las cuentas demo (contraseña `Eyesofter123!`) existen solo en desarrollo. En producción el portal
  desactiva al arrancar cualquier cuenta demo que conserve esa contraseña.

