using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Seguridad;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);
// No anunciar la tecnología del servidor en las respuestas (cabecera "Server: Kestrel").
builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);

// Railway asigna el puerto en la variable PORT.
var puerto = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(puerto)) builder.WebHost.UseUrls($"http://0.0.0.0:{puerto}");

// ---------- Cifrado de datos sensibles (C-RC-04) ----------
using var logInicio = LoggerFactory.Create(l => l.AddConsole());
var cifrado = CifradoDatos.DesdeConfiguracion(builder.Configuration, builder.Environment, logInicio.CreateLogger("Inicio"));
builder.Services.AddSingleton(cifrado);

// ---------- Filtro por escuela (C-RE-01): lo usa el DbContext en cada consulta ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AlcanceEscuela>();

// ---------- EF Core + PostgreSQL (base compartida con el servidor de la app móvil) ----------
var cadena = BaseDeDatos.CadenaConexion(builder.Configuration);
builder.Services.AddDbContext<ApplicationDbContext>(o => BaseDeDatos.Configurar(o, cadena));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Las llaves que protegen las cookies se guardan en la base: si Railway reinicia el
// contenedor, las sesiones y los enlaces para crear la contraseña siguen siendo válidos.
builder.Services.AddDataProtection()
    .SetApplicationName("EyesofterWeb")
    .PersistKeysToDbContext<ApplicationDbContext>();

// ---------- Identity: cuentas individuales y roles ----------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;

        // C-RC-05: Identity guarda las contraseñas con PBKDF2-HMAC-SHA512 (100 000 iteraciones), nunca en claro.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        // C-RFNN-01: bloqueo tras 3 intentos fallidos. Mismo criterio que el servidor de la app
        // (comparten las columnas access_failed_count y lockout_end).
        options.Lockout.MaxFailedAccessAttempts = 3;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    // Las escuelas de la cuenta viajan como claims y se recalculan en cada petición.
    .AddClaimsPrincipalFactory<FabricaClaimsEyesofter>()
    .AddErrorDescriber<IdentityErroresEspanol>()
    .AddDefaultTokenProviders();

// ---------- Iniciar sesión con Google (opcional: solo si hay GOOGLE_CLIENT_ID y GOOGLE_CLIENT_SECRET) ----------
// Google dice quién es la persona; la cuenta debe existir ya en el portal (ver Servicios/AccesoGoogle.cs).
var (googleId, googleSecreto) = AccesoGoogle.Credenciales(builder.Configuration);
if (googleId is not null && googleSecreto is not null)
{
    builder.Services.AddAuthentication().AddGoogle(AccesoGoogle.Proveedor, o =>
    {
        o.ClientId = googleId;
        o.ClientSecret = googleSecreto;
        // URI de redireccionamiento autorizada en Google Cloud: https://<dominio>/signin-google
        o.CallbackPath = "/signin-google";
        o.SignInScheme = IdentityConstants.ExternalScheme;
        // Solo nombre y correo (scopes openid, profile, email). No se pide acceso a Gmail ni a Drive.
        o.ClaimActions.MapJsonKey(AccesoGoogle.ClaimCorreoVerificado, "email_verified");
        // Google regresa con una navegación GET normal: Lax basta (y funciona en http://localhost).
        o.CorrelationCookie.SameSite = SameSiteMode.Lax;
        o.CorrelationCookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.Events.OnRedirectToAuthorizationEndpoint = ctx =>
        {
            // Computadoras compartidas de la escuela: que siempre pregunte qué cuenta de Google usar.
            ctx.Response.Redirect(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(ctx.RedirectUri, "prompt", "select_account"));
            return Task.CompletedTask;
        };
        o.Events.OnRemoteFailure = ctx =>
        {
            // La persona canceló en Google o el enlace de regreso ya no sirve: de vuelta al login con un aviso.
            ctx.Response.Redirect("/Cuenta/Login?google=cancelado");
            ctx.HandleResponse();
            return Task.CompletedTask;
        };
    });
}

builder.Services.AddAuthorization(o => o.AddPolicy(Roles.PoliticaGlobal,
    p => p.RequireRole(Roles.Administrador).RequireClaim(AlcanceEscuela.ClaimGlobal, "true")));

// Los enlaces para crear la contraseña (alta de cuentas) caducan en 2 horas.
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));

// Revisa el sello de seguridad en cada petición: si se cambia el rol o se desactiva
// a alguien, el cambio se aplica de inmediato.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

var minutosInactividad = builder.Configuration.GetValue("Eyesofter:MinutosInactividad", 15);
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Cuenta/Login";
    options.LogoutPath = "/Cuenta/Logout";
    options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
    // W-RFNN-01: la sesión se cierra sola tras 15 minutos sin actividad.
    options.ExpireTimeSpan = TimeSpan.FromMinutes(minutosInactividad);
    options.SlidingExpiration = true;
    options.Cookie.Name = "eyesofter.sesion";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "eyesofter.af";
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

// Límite de intentos por IP en el inicio de sesión y la recuperación de contraseña.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("acceso", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Railway pone un proxy delante: hay que confiar en X-Forwarded-Proto para saber que la petición fue HTTPS.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Emite acentos y ñ tal cual en el HTML (sin convertirlos a entidades &#xE9;).
builder.Services.Configure<WebEncoderOptions>(options =>
{
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All);
});

// ---------- Rendimiento (Lighthouse): comprimir CSS, JS, fuentes e íconos ----------
// Solo archivos estáticos: el HTML lleva el token antifalsificación y no se comprime sobre HTTPS (BREACH).
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    o.MimeTypes = ArchivosEstaticos.TiposComprimibles;
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);

// ---------- Servicios propios ----------
builder.Services.AddHttpClient();
builder.Services.Configure<OpcionesEyesofter>(builder.Configuration.GetSection("Eyesofter"));
builder.Services.AddScoped<Auditoria>();
builder.Services.AddScoped<Escuelas>();
builder.Services.AddScoped<GruposEscolares>();
builder.Services.AddScoped<Notificador>();
builder.Services.AddScoped<Expedientes>();
builder.Services.AddScoped<Indicadores>();
builder.Services.AddScoped<Documentos>();
builder.Services.AddScoped<ICorreo, CorreoConfigurado>(); // Brevo o Resend, según las variables
builder.Services.AddScoped<ISms, SmsTwilio>();
builder.Services.AddScoped<RecuperacionPassword>();
builder.Services.AddScoped<AccesoGoogle>();
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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
    // C-RC-08: el tutor acepta el aviso de privacidad antes de ver datos de sus hijos.
    options.Filters.Add<FiltroConsentimiento>();
    // Dirección y Enfermería deben activar la verificación en dos pasos antes de ver datos.
    options.Filters.Add<FiltroDosPasos>();
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
app.UseForwardedHeaders();
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // C-RC-03: el navegador solo habla con el portal por HTTPS (TLS 1.2+ lo pone Railway).
    app.UseHsts();
    // El healthcheck de Railway llega por HTTP desde la red interna: /salud no se redirige.
    app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/salud"), rama => rama.UseHttpsRedirection());
}
app.UseStatusCodePagesWithReExecute("/Home/Estado/{0}");

app.UseCabecerasSeguridad();
app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = ArchivosEstaticos.PonerCache });

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// security.txt (RFC 9116): cómo reportar una vulnerabilidad de forma responsable.
app.MapGet("/.well-known/security.txt", (Microsoft.Extensions.Options.IOptions<OpcionesEyesofter> o) =>
{
    var web = o.Value.UrlWeb.TrimEnd('/');
    return Results.Text(
        $"Contact: {web}/Soporte/Reportar\n" +
        $"Expires: {DateTime.UtcNow.AddYears(1):yyyy-MM-ddT00:00:00Z}\n" +
        "Preferred-Languages: es, en\n" +
        $"Policy: {web}/Privacidad/Seguridad\n", "text/plain; charset=utf-8");
});

// Healthcheck para Railway (no toca la base para no reiniciar el servicio si la base tarda).
app.MapGet("/salud", () => Results.Ok(new { status = "ok" }));

// ---------- Migraciones y datos iniciales ----------
try
{
    await DbSeeder.SeedAsync(app.Services, app.Logger, app.Environment, app.Configuration);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "No se pudo preparar la base de datos. Revisa la cadena de conexión (DATABASE_URL).");
}

app.Run();

/// <summary>Para las pruebas de integración (WebApplicationFactory).</summary>
public partial class Program { }
