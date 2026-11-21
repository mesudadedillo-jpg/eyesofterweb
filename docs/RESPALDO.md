# Respaldo y recuperación de la base de datos (C-RC-13, C-RS-05)

La información del sistema vive en una sola base PostgreSQL en Railway. Hay dos respaldos
complementarios:

| Respaldo | Frecuencia | Dónde queda | Quién lo hace |
|---|---|---|---|
| **Automático** (GitHub Actions, `.github/workflows/respaldo.yml`) | Diario, 02:17 h | Artefacto cifrado del workflow, 30 días | Solo |
| **Manual** (antes de cambios grandes o del inicio del piloto) | Cuando se necesite | La computadora del responsable | Equipo de desarrollo |

Los datos sensibles **ya van cifrados dentro de la base** (C-RC-04). El archivo de respaldo además
se cifra completo con AES-256. Para usar un respaldo hacen falta **dos secretos**: la contraseña del
respaldo (`RESPALDO_CLAVE`) y la llave de datos (`EYESOFTER_CLAVE_DATOS`). Guárdalas fuera de
GitHub, por ejemplo en un gestor de contraseñas, con acceso de Dirección y del responsable técnico.

## 0. Dónde vive el respaldo: un repositorio PRIVADO

Este repositorio es público (portafolio). En un repositorio público **cualquier cuenta de GitHub puede
descargar los artefactos** de los workflows, así que el respaldo diario **no corre aquí**: el workflow
primero pregunta a GitHub si el repositorio es privado y, si no lo es, no hace nada.

Para respaldar la base de producción:

1. GitHub → *New repository* → nombre `eyesofter-respaldos`, **Private**, sin README.
2. Copia ahí `.github/workflows/respaldo.yml` (mismo contenido, misma ruta).
3. En **ese** repositorio privado agrega los secretos `DATABASE_PUBLIC_URL` y `RESPALDO_CLAVE` (paso 1 de abajo).
   Si los habías puesto en este repositorio público, **bórralos de aquí**.
4. *Actions → Respaldo diario de la base → Run workflow* para probarlo.

La URL pública de PostgreSQL deja la base accesible desde internet con usuario y contraseña. Usa una
contraseña larga (la genera Railway) y, si no vas a usar el respaldo automático, desactiva el acceso
público (*TCP Proxy*) del servicio Postgres en Railway.

## 1. Activar el respaldo automático (una sola vez)

1. Railway → servicio **Postgres** → *Variables* → copia `DATABASE_PUBLIC_URL` (la URL pública;
   la interna `*.railway.internal` no es accesible desde GitHub).
2. GitHub → repositorio **privado** `eyesofter-respaldos` → *Settings → Secrets and variables → Actions → New repository secret*:
   - `DATABASE_PUBLIC_URL` = la URL del paso 1
   - `RESPALDO_CLAVE` = una contraseña larga (por ejemplo, la salida de `openssl rand -base64 24`)
3. *Actions → Respaldo diario de la base → Run workflow* para probarlo. Debe terminar en verde y
   dejar un artefacto `respaldo-…` con el archivo `eyesofter-AAAA-MM-DD.dump.gpg`.

Si un respaldo falla, GitHub manda un correo al dueño del repositorio.

## 2. Respaldo manual

Requiere el cliente de PostgreSQL (versión igual o mayor que la del servidor) y GnuPG.

```bash
pg_dump "<DATABASE_PUBLIC_URL>" --format=custom --no-owner --no-privileges --file=eyesofter.dump
```

```bash
gpg --symmetric --cipher-algo AES256 -o eyesofter-$(date +%F).dump.gpg eyesofter.dump
```

Borra `eyesofter.dump` (sin cifrar) después de cifrarlo.

## 3. Recuperación

1. Descarga el artefacto (*Actions → la ejecución → Artifacts*) y descomprímelo.
2. Descifra (pide `RESPALDO_CLAVE`):

```bash
gpg -o eyesofter.dump -d eyesofter-AAAA-MM-DD.dump.gpg
```

3. **Prueba primero en una base nueva**, nunca directo sobre producción:

```bash
createdb eyesofter_restaurada
```

```bash
pg_restore --no-owner --no-privileges -d "postgresql://usuario:clave@host:puerto/eyesofter_restaurada" eyesofter.dump
```

4. Revisa que tenga datos:

```bash
psql "postgresql://usuario:clave@host:puerto/eyesofter_restaurada" -c "select count(*) from alumnos; select count(*) from casos;"
```

5. Para **reemplazar producción** (pérdida total o corrupción):
   1. Pausa los dos servicios en Railway (*Settings → Pause*) para que nadie escriba durante la restauración.
   2. Crea un PostgreSQL nuevo en Railway y restaura ahí con `pg_restore` (usa su `DATABASE_PUBLIC_URL`).
   3. En **los dos** servicios cambia `DATABASE_URL` a `${{<nuevo Postgres>.DATABASE_URL}}`.
   4. Conserva la **misma** `EYESOFTER_CLAVE_DATOS`: sin ella los datos cifrados no se pueden leer.
   5. Reanuda los servicios y revisa `/salud` y el inicio de sesión.

Tiempo estimado con los datos del piloto: menos de 15 minutos.

## 4. Prueba periódica

Una vez al mes, y antes del piloto, restaura el último respaldo en una base de prueba (paso 3) y
revisa que tenga datos. El procedimiento se probó el 20/11/2026: respaldo cifrado, descifrado y
restauración con los mismos conteos de cuentas, tamizajes y casos que el original.

## 5. Qué NO está en el respaldo

- Las variables de Railway (llaves y secretos): anótalas aparte.
- El código: está en GitHub.
- Los tokens de push de los teléfonos se regeneran solos al iniciar sesión.
