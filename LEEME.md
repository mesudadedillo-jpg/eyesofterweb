# EYESOFTER Web — Actividad 4 (ASP.NET Core MVC)

Módulo web de seguimiento de casos de salud visual escolar.

- **ASP.NET Core MVC** (.NET 10), patrón Modelo-Vista-Controlador
- **Entity Framework Core + SQL Server**, base creada con **migraciones** (ya incluidas en `EyesofterWeb/Migrations`)
- **ASP.NET Core Identity**: registro, inicio y cierre de sesión con cuentas individuales
- **3 roles** (Administrador, Enfermería, Docente) aplicados en el servidor con `[Authorize(Roles = ...)]`
- **CRUD** de 5 tablas relacionadas, con reglas de negocio
- **Data Annotations** (mensajes en español) y **Bootstrap 5.3** local en `wwwroot/lib` (funciona sin internet)

## Requisitos

- **SDK de .NET 10**
- **SQL Server LocalDB** (viene con Visual Studio, carga "Desarrollo de ASP.NET y web") o SQL Server Express

## Ejecutar con Visual Studio

1. Abre `EyesofterWeb.sln`.
2. **Herramientas → Administrador de paquetes NuGet → Consola del Administrador de paquetes** y escribe:
   ```
   Update-Database
   ```
   Esto aplica la migración `Inicial` y crea la base `EyesofterWebDb` en `(localdb)\mssqllocaldb`.
3. Presiona **F5** (perfil *http*). Se abre http://localhost:5080.

## Ejecutar desde la terminal

```bash
cd EyesofterWeb
dotnet tool install --global dotnet-ef
dotnet ef database update
dotnet run
```

Abre http://localhost:5080.

> Si usas SQL Server Express en lugar de LocalDB, cambia en `EyesofterWeb/appsettings.json`
> `Server=(localdb)\\mssqllocaldb` por `Server=.\\SQLEXPRESS`.

## Cuentas de prueba

Se crean solas la primera vez que arranca la app (junto con datos ficticios de ejemplo):

| Rol | Correo | Contraseña |
|---|---|---|
| Administrador | admin@eyesofter.mx | Admin123! |
| Enfermería | enfermeria@eyesofter.mx | Enfermeria123! |
| Docente | docente@eyesofter.mx | Docente123! |

La página **Permisos** (menú superior) muestra qué puede hacer cada rol. Para comprobar que la restricción está en el
servidor, entra como Docente y escribe a mano `http://localhost:5080/Tutores`: responde **Acceso denegado (403)**.

## Estructura

```
EyesofterWeb/
├─ Controllers/   Home, Cuenta (Identity), Usuarios (roles), Tutores, Alumnos, Clinicas, Casos, Eventos
├─ Data/          ApplicationDbContext (EF Core + Identity), DbSeeder, mensajes de Identity en español
├─ Migrations/    Migración Inicial generada con dotnet ef migrations add
├─ Models/        Entidades, enums, Roles, ViewModels
├─ Validation/    Data Annotation propia [FechaNoFutura]
├─ Views/         Vistas Razor con Bootstrap 5
├─ wwwroot/       CSS/JS propios y librerías locales (Bootstrap, Bootstrap Icons, jQuery Validation)
└─ Program.cs     EF Core + SQL Server, Identity, cookies, roles, MVC
```

Tablas del dominio: `Tutores` 1–N `Alumnos` 1–N `Casos` N–1 `Clinicas`; `Casos` 1–N `EventosCaso`.

## Problemas comunes

| Mensaje | Solución |
|---|---|
| `dotnet ef` no se reconoce | `dotnet tool install --global dotnet-ef` y abre una terminal nueva |
| *A network-related or instance-specific error* | LocalDB no está instalado o apagado: `sqllocaldb start MSSQLLocalDB` |
| El tablero dice "La base de datos no está lista" | Falta `Update-Database` / `dotnet ef database update` |
| No puedo entrar con una cuenta de prueba | Se bloqueó por 3 intentos: espera 5 minutos o desbloquéala como Admin en *Usuarios* |
| Empezar de cero | `dotnet ef database drop` y luego `dotnet ef database update` |
