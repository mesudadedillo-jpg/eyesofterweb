# Matriz de trazabilidad de requerimientos — EYESOFTER Suite

Relaciona los 119 requisitos de *Identificación de requerimientos EYESOFTER Suite* con su
implementación en los tres repositorios:

| Abreviatura | Repositorio | Qué es |
|---|---|---|
| **WEB** | `eyesofterweb` | Portal web ASP.NET Core 10 (dueño de la base y de las cuentas) |
| **API** | `eyesofterandroid` | Servidor Ktor de la app móvil y del escritorio, despachador de notificaciones |
| **APP** | `eyesofterandroidfront` | App móvil Kotlin Multiplatform (Android; iOS preparado) |
| **ESC** | `Eyesofter` | Sistema de escritorio JavaFX: examen de agudeza y color, y envío del resultado a la API |

Estado: ✅ cumplido y probado · 🟡 cumplido, pero depende de una configuración o de otro sistema ·
📄 se cumple con un documento o un proceso del equipo · ⚠️ difiere de lo pedido (ver nota).

Pruebas: API, 43 pruebas de punta a punta contra PostgreSQL (`./gradlew test`). APP, 14 pruebas
(`./gradlew :app:shared:jvmTest`). WEB, un recorrido automatizado con navegador (roles, QR, archivos,
cabeceras) más la integración WEB↔API sobre la misma base.

## 4.1 Portal web

| ID | Estado | Dónde / cómo |
|---|---|---|
| W-RFN-01 | ✅ | El caso se abre solo al registrar un amarillo/rojo, desde el portal (`CasosController.Create`) o desde el escritorio (`API repos/Expedientes.kt → ingestar`) |
| W-RFN-02 | ✅ | `eventos_caso`: tamizaje, aviso al tutor, cita, comprobante, destino, atención, cierre y reapertura. Vista *Casos → Detalle* |
| W-RFN-03 | ✅ | *Casos*: filtros por grado, grupo, estado, semáforo y campaña |
| W-RFN-04 | ✅ | *Casos → Cerrar* exige atención registrada o comprobante aprobado (`Expedientes.PuedeCerrarAsync`) |
| W-RFN-05 | ✅ | *Tutores* y *Alumnos*: un tutor con varios alumnos. *Usuarios*: la cuenta se liga al tutor |
| W-RFN-06 | ✅ | *Mis hijos → Reporte*: textos de `Helpers/TextosSemaforo.cs`, sin diagnóstico |
| W-RFN-07 | ✅ | Semáforo con color, texto e icono |
| W-RFN-08 | ✅ | *Mis hijos*: pase con QR (asociada) o resumen (no asociada), con PDF |
| W-RFN-09 | ✅ | *Comprobantes → Revisar*: aprobar o rechazar |
| W-RFN-10 | ✅ | *Casos → Atención manual* a partir del comprobante validado |
| W-RFN-11 | ✅ | *Pases → Leer pase* (especialista): escanea el QR y registra la atención |
| W-RFN-12 | ✅ | *Clínicas*: alta, edición, desactivación, coordenadas y contacto |
| W-RFN-13 | ✅ | *Clínicas*: casilla "Asociada al programa" |
| W-RFN-14 | ✅ | Tablero: % de alumnos evaluados por grado y grupo |
| W-RFN-15 | ✅ | Tablero: severidad primaria vs. secundaria |
| W-RFN-16 | ✅ | Tablero: comparativo entre campañas y ciclos (*Campañas*) |
| W-RFN-17 | ✅ | Tablero: días promedio de detección a atención |
| W-RFN-18 | ✅ | Tablero: tasa de casos resueltos |
| W-RFNN-01 | ✅ | Cookie de 15 min con renovación por uso y aviso previo en pantalla (`site.js`) |
| W-RFNN-02 | ✅ | Rechazo con motivo obligatorio; el tutor recibe aviso para reenviar |
| W-RFNN-03 | ✅ | Pase inválido, vencido, cancelado o usado: mensaje claro (`Expedientes.ValidarPaseAsync`) |
| W-RFNN-04 | ✅ | Solo especialistas de una clínica asociada y activa, y solo para pases de SU clínica |
| W-RI-01 | ✅ | Bootstrap responsivo; recorrido automatizado a 375, 820 y 1440 px en 23 pantallas: sin desplazamiento horizontal |
| W-RI-02 | ✅ | Cámara del navegador (html5-qrcode) o captura manual del código |
| W-RI-03 | ✅ | Tablero y casos en PDF (QuestPDF) y Excel (ClosedXML) |
| W-RI-04 | ✅ | PDF del pase con QR y del resumen |
| W-RC-01 | ✅ | Indicadores en consultas agregadas; medido en local < 1 s con los datos demo |
| W-RC-02 | ✅ | Inicio de Enfermería → "Casos abiertos" (1 clic) |
| W-RC-03 | ✅ | Auditoría automática axe-core (WCAG 2.0/2.1 A y AA) el 21/11/2026 sobre 23 pantallas de los 5 roles: **0 infracciones** (se corrigieron contraste del menú y pie, tablas desplazables con foco y roles de íconos). Sigue siendo recomendable una prueba manual con lector de pantalla |
| W-RC-04 | 🟡 | HTML/CSS/JS estándar sin APIs experimentales; probado en Chromium. Falta probar en Safari |
| W-RE-01 | ✅ | Ya existen el rol Especialista, `citas` y `atenciones` por clínica: el portal de clínicas se agrega sobre ellos |
| W-RE-02 | ✅ | Alumnos y grupos con matrícula única: una importación se conecta por matrícula. El escritorio ya consulta `GET /api/escritorio/alumnos` |
| W-RS-01 | 📄 | [`MANUAL_USUARIO.md`](MANUAL_USUARIO.md) |
| W-RS-02 | 📄 | [`CAPACITACION.md`](CAPACITACION.md): temario y lista de verificación |

## 4.2 Aplicación móvil

| ID | Estado | Dónde / cómo |
|---|---|---|
| M-RFN-01 | ✅ | Inicio de sesión con las cuentas del portal (`LoginScreen.kt`) |
| M-RFN-02 | 🟡 | Push por FCM (`PushAndroid.kt`, API `CanalPushFcm.kt`). Se activa al crear el proyecto de Firebase; mientras, bandeja y correo |
| M-RFN-03 | ✅ | `TamizajeScreen.kt`: resultado, observaciones y qué hacer |
| M-RFN-04 | ✅ | Semáforo verde, amarillo y rojo con texto e icono |
| M-RFN-05 | ✅ | Cámara o archivo; se sube cifrado |
| M-RFN-06 | ✅ | Mapa 3D MapLibre de Tulancingo (`DirectorioMapa.kt`) |
| M-RFN-07 | ✅ | Insignia "Asociada" y asociadas primero en el mapa y la lista |
| M-RFN-08 | ✅ | `SolicitarCitaScreen`; avisa a Enfermería y a la clínica |
| M-RFN-09 | ✅ | `ElegirDestinoScreen`: del directorio o escrita a mano |
| M-RFN-10 | ✅ | Pase con QR (`PaseMedico`) |
| M-RFN-11 | ✅ | Resumen sin QR (`ResumenTamizaje`) |
| M-RFN-12 | ✅ | `PausaVisualScreen.kt`: temporizador 20-20-20 |
| M-RFN-13 | ✅ | Ejercicios guiados de parpadeo, enfoque y relajación |
| M-RFNN-01 | ✅ | Último reporte guardado y aviso "puede no estar actualizado" (`AlmacenLocal.kt`) |
| M-RFNN-02 | ✅ | Cola de comprobantes; se envía sola al volver la red |
| M-RFNN-03 | ✅ | QR y resumen guardados en el teléfono |
| M-RFNN-04 | 🟡 | Pendientes al iniciar sesión ✅; correo de respaldo ✅ en cuanto se configure Resend |
| M-RFNN-05 | ✅ | Sin permiso de ubicación: lista con asociadas primero |
| M-RFNN-06 | ✅ | Se verifica el nombre en el directorio, se informa que no es asociada y se ofrece el resumen |
| M-RI-01 | 🟡 | `minSdk` 24 (Android 7) cubre Android 8+ ✅. iOS: el código es multiplataforma, falta compilarlo en una Mac |
| M-RI-02 | 🟡 | FCM HTTP v1 para Android e iOS; falta el proyecto de Firebase y la llave APNs |
| M-RI-03 | ✅ | MapLibre con mapas de OpenFreeMap |
| M-RI-04 | ✅ | Cámara del teléfono (`TelefonoAndroid.kt`) |
| M-RI-05 | ✅ | Ubicación con permiso del usuario |
| M-RI-06 | ✅ | Vibración y sonido en la pausa 20-20-20 |
| M-RC-01 | 🟡 | El despachador entrega en ≤ 3 s después del registro; depende de Firebase |
| M-RC-02 | ✅ | Notificación → resultado en 1 toque (`rutaAScreen`) |
| M-RC-03 | ✅ | Panel del alumno con lenguaje e ilustraciones para su edad |
| M-RC-04 | ✅ | Compose adaptable; capturas a 412 dp |
| M-RC-05 | ✅ | Fotos reducidas antes de subir (`TelefonoAndroid.kt`) |
| M-RE-01 | ✅ | Ejercicios como lista de datos en `PausaVisualScreen.kt`; agregar uno no cambia la arquitectura |
| M-RS-01 | ✅ | `GuiaScreen` para tutores y para alumnos (y para el personal) |

## 4.3 Comunes

| ID | Estado | Dónde / cómo |
|---|---|---|
| C-RFN-01 | ✅ | Roles Dirección, Enfermería, Tutor, Alumno y Especialista (Identity), los mismos en la web y la app |
| C-RFN-02 | ✅ | `POST /api/escritorio/tamizajes` (API §13). El escritorio (`Eyesofter/…/sincronizacion`) envía cada tamizaje terminado con la matrícula del alumno; probado de punta a punta contra la API y visible en *Mis hijos* del tutor |
| C-RFN-03 | ✅ | Tabla `clinicas` con asociada/no asociada y activa |
| C-RFN-04 | ✅ | Se verifica la asociación antes de crear el pase (WEB y API) |
| C-RFN-05 | ✅ | Código de 128 bits único por caso, un solo uso, 60 días |
| C-RFN-06 | ✅ | Resumen: fecha, semáforo y observaciones, sin diagnóstico ni QR |
| C-RFN-07 | ✅ | `leida_en` guarda la primera apertura (WEB y APP) |
| C-RFN-08 | ✅ | Recordatorio de aviso sin leer y de caso sin atender (`Recordatorios.kt`) |
| C-RFNN-01 | ✅ | 3 intentos → 15 min, compartido entre WEB y APP (columnas de Identity) |
| C-RFNN-02 | 🟡 | Enlace por correo de 2 h (`Cuenta/Olvide`); requiere `RESEND_API_KEY` |
| C-RFNN-03 | ✅ | El escritorio guarda una cola en SQLite y reintenta con espera creciente (30 s → 30 min); tras 30 min de falla avisa a Enfermería con `/api/escritorio/incidencias`. El folio es idempotente: reenviar no duplica (probado) |
| C-RFNN-04 | ✅ | A los 3 días: reenvío al tutor y alerta a Enfermería |
| C-RFNN-05 | ✅ | Solo JPG, PNG, WEBP o PDF (por contenido) y hasta 10 MB, con el motivo |
| C-RFNN-06 | ✅ | 404 y registro `ACCESO_DENEGADO` en la bitácora (WEB y API) |
| C-RFNN-07 | ✅ | Clínica no registrada = no asociada → solo resumen |
| C-RI-01 | ✅ | REST/JSON; HTTPS en Railway |
| C-RI-02 | 🟡 | Resend para recuperación y avisos; requiere `RESEND_API_KEY` y un dominio verificado |
| C-RC-01 | ✅ | Consultas indexadas; con 100 usuarios simultáneos el p95 fue de 545 ms (< 2 s) |
| C-RC-02 | ✅ | Misma base: el cambio se ve al instante; la app revisa cada 5 s |
| C-RC-03 | ✅ | TLS de Railway, HSTS en la web, la app de release sin HTTP |
| C-RC-04 | ✅ | AES-256-GCM en agudezas, observaciones, línea de tiempo, valoraciones y archivos; misma llave en WEB y API |
| C-RC-05 | ✅ | PBKDF2-HMAC-SHA512 de 100 000 iteraciones (Identity); nunca en texto plano |
| C-RC-06 | ✅ | El tutor solo ve a sus hijos (probado en WEB y API) |
| C-RC-07 | ✅ | Tabla `auditoria` (WEB y API); vista *Auditoría* para Dirección |
| C-RC-08 | ✅ | Aceptación obligatoria del aviso (WEB y APP), registrada con versión, fecha, medio e IP |
| C-RC-09 | ✅ | Textos en lenguaje claro, revisados para no usar términos técnicos |
| C-RC-10 | ✅ | Color + texto + icono |
| C-RC-11 | ✅ | El tamizaje se guarda tal como llega del escritorio (solo se cifra) |
| C-RC-12 | 🟡 | Healthchecks y reinicio automático en Railway; la disponibilidad depende del plan contratado |
| C-RC-13 | 🟡 | Respaldo diario cifrado con GitHub Actions ([`RESPALDO.md`](RESPALDO.md)); hay que cargar los secretos |
| C-RC-14 | ✅ | Prueba de carga local (20/11/2026): 100 usuarios a la vez, 2000 peticiones de expediente, bandeja y directorio → 511 por segundo, mediana 140 ms, p95 545 ms, sin errores. Repetirla en Railway antes del piloto |
| C-RC-15 | ✅ | Módulos por capa, documentación y Git en los tres repositorios |
| C-RE-01 | 🟡 | Tablas `planteles` y campañas por plantel; falta separar la vista por institución en el portal |
| C-RE-02 | ✅ | Interfaz `CanalNotificacion`: SMS o WhatsApp se agregan como un canal más |
| C-RE-03 | 🟡 | Cultura es-MX y textos en un solo lugar por pantalla; falta pasarlos a archivos de recursos |
| C-RE-04 | ✅ | Docker y variables de entorno: cambiar de plan en Railway no cambia el código |
| C-RP-01 | 📄 | Piloto con la escuela (actividad del equipo) |
| C-RP-02 | ✅ | El sistema no hace el examen de agudeza; solo recibe el resultado del escritorio |
| C-RP-03 | ⚠️ | Se usó ASP.NET Core (web), Kotlin Multiplatform (app) y Ktor + PostgreSQL (servidor), que son de código abierto pero no las tecnologías que nombra el requisito. **Acordar con el profesor y ajustar el documento** |
| C-RP-04 | ⚠️ | Se usa Railway (tiene capa gratuita) en lugar de AWS, Supabase o Render. **Ajustar el documento** |
| C-RP-05 | 📄 | Infraestructura de la escuela |
| C-RP-06 | ✅ | Funciona en los equipos y teléfonos existentes; no requiere comprar nada |
| C-RP-07 | 📄 | Proceso del equipo |
| C-RP-08 | 📄 | Calendario del curso |
| C-RP-09 | 🟡 | *Clínicas* en el portal; hay que capturar las clínicas reales antes del piloto |
| C-RS-01 | 📄 | [`MANUAL_TECNICO.md`](MANUAL_TECNICO.md) |
| C-RS-02 | ✅ | `eyesofterandroid/docs/API.md` |
| C-RS-03 | ✅ | Errores en los logs de Railway (WEB y API) y fallas en la tabla `auditoria` |
| C-RS-04 | ✅ | *Soporte → Reportar* (WEB) y *Reportar* (APP); llegan a Dirección |
| C-RS-05 | 📄 | [`RESPALDO.md`](RESPALDO.md) |
| C-RS-06 | 📄 | Proceso del equipo; los reportes llegan a *Soporte → Reportes* |
| C-RS-07 | ✅ | Pie de página de la web; login, inicio y guía de la app |
| C-RS-08 | ✅ | *Soporte → Guía para clínicas* (escaneo del QR y registro de la atención) |
