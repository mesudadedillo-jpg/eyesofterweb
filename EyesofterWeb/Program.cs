using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.WebEncoders;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Base de datos: Entity Framework Core + SQL Server ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'DefaultConnection'.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ---------- Autenticación: ASP.NET Core Identity con cuentas individuales y roles ----------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        // Bloqueo tras 3 intentos fallidos durante 5 minutos
        options.Lockout.MaxFailedAccessAttempts = 3;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddErrorDescriber<IdentityErroresEspanol>()
    .AddDefaultTokenProviders();

// Revisa el sello de seguridad en cada petición: si el Administrador cambia el rol
// de alguien, el cambio se aplica de inmediato sin que tenga que volver a entrar.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Cuenta/Login";
    options.LogoutPath = "/Cuenta/Logout";
    options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Emite acentos y ñ tal cual en el HTML (sin convertirlos a entidades &#xE9;).
builder.Services.Configure<WebEncoderOptions>(options =>
{
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All);
});

// ---------- MVC ----------
builder.Services.AddControllersWithViews(options =>
{
    // Mensajes de enlace de modelo en español (por ejemplo, cuando un número viene vacío)
    var m = options.ModelBindingMessageProvider;
    m.SetValueMustNotBeNullAccessor(_ => "Este campo es obligatorio.");
    m.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"El valor '{valor}' no es válido para {campo}.");
    m.SetMissingBindRequiredValueAccessor(campo => $"Falta un valor para {campo}.");
    m.SetValueIsInvalidAccessor(valor => $"El valor '{valor}' no es válido.");
    m.SetValueMustBeANumberAccessor(campo => $"El campo {campo} debe ser un número.");
    m.SetUnknownValueIsInvalidAccessor(campo => $"El valor indicado no es válido para {campo}.");
    m.SetMissingKeyOrValueAccessor(() => "Este campo es obligatorio.");
    m.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => $"El valor '{valor}' no es válido.");
    m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "El valor indicado no es válido.");
    m.SetNonPropertyValueMustBeANumberAccessor(() => "El campo debe ser un número.");
});

var app = builder.Build();

// ---------- Cultura es-MX (fechas y textos) ----------
var cultura = new CultureInfo("es-MX");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultura),
    SupportedCultures = new List<CultureInfo> { cultura },
    SupportedUICultures = new List<CultureInfo> { cultura }
});

// ---------- Canal de peticiones ----------
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ---------- Datos iniciales: roles, usuarios de prueba y ejemplos ----------
try
{
    await DbSeeder.SeedAsync(app.Services, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "No se pudieron cargar los datos iniciales. Revisa la cadena de conexión y ejecuta 'dotnet ef database update'.");
}

app.Run();
