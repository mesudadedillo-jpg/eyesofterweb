#!/usr/bin/env bash
# Auditoría Lighthouse del portal en MÓVIL y ESCRITORIO (W-RC-03 accesibilidad, rendimiento, buenas prácticas, SEO).
#
# Uso:   scripts/lighthouse.sh [URL_BASE] [CARPETA_SALIDA]
# Ej.:   scripts/lighthouse.sh https://localhost:7080            (portal local con `dotnet run --launch-profile https`)
#        scripts/lighthouse.sh https://<tu-dominio>.up.railway.app
#
# Requisitos: Node 18+ y Google Chrome/Chromium (o define CHROME_PATH). Descarga Lighthouse con npx la primera vez.
# Deja un reporte HTML y JSON por página y modo, e imprime la tabla de calificaciones.
# Termina con error si alguna categoría queda abajo de MINIMO (por defecto 90).
set -euo pipefail

BASE="${1:-https://localhost:7080}"
OUT="${2:-lighthouse-reportes}"
MINIMO="${MINIMO:-90}"
PAGINAS=("Cuenta/Login" "" "App" "Privacidad/Seguridad" "Cuenta/Olvide" "Soporte/Ayuda")
mkdir -p "$OUT"
falla=0

printf "%-22s %-11s %5s %5s %5s %5s\n" "Página" "Modo" "Rend" "Acces" "BuenP" "SEO"
for ruta in "${PAGINAS[@]}"; do
  nombre=$(echo "${ruta:-inicio}" | tr '/' '-')
  for modo in movil escritorio; do
    extra=(); [ "$modo" = escritorio ] && extra=(--preset=desktop)
    npx --yes lighthouse@12 "${BASE%/}/$ruta" "${extra[@]}" --quiet --output=json --output=html \
      --output-path="$OUT/$nombre-$modo" \
      --chrome-flags="--headless=new --no-sandbox --ignore-certificate-errors" >/dev/null 2>&1
    linea=$(node -e "
      const c = require(require('path').resolve('$OUT/$nombre-$modo.report.json')).categories;
      const v = ['performance','accessibility','best-practices','seo'].map(k => Math.round(c[k].score * 100));
      console.log(v.join(' '));")
    read -r rend acce buen seo <<<"$linea"
    printf "%-22s %-11s %5s %5s %5s %5s\n" "$nombre" "$modo" "$rend" "$acce" "$buen" "$seo"
    for v in $rend $acce $buen $seo; do [ "$v" -lt "$MINIMO" ] && falla=1; done
  done
done
[ $falla -eq 0 ] && echo "OK: todas las categorías ≥ $MINIMO." || { echo "Hay categorías abajo de $MINIMO: revisa los reportes en $OUT/"; exit 1; }
