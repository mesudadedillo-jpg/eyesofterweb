using System.Security.Claims;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EyesofterWeb.Seguridad
{
    /// <summary>
    /// Qué escuelas puede ver quien hace la petición. Es la ÚNICA fuente del filtro por escuela:
    /// <see cref="ApplicationDbContext"/> lo aplica como filtro global a alumnos, tutores,
    /// tamizajes, casos, comprobantes, citas, pases, campañas y convenios. Así, una pantalla
    /// nueva que olvide filtrar no puede mostrar datos de otra escuela.
    ///
    ///  - Dirección o Enfermería: solo sus escuelas (tabla usuario_planteles). Sin escuelas = nada.
    ///  - Dirección con "administrador global": todas.
    ///  - Tutor, alumno y especialista: sin filtro por escuela; esas pantallas ya filtran por SU
    ///    tutor, alumno o clínica (C-RC-06), y un tutor puede tener hijos en varias escuelas.
    ///  - Sin petición web (arranque, migraciones): sin filtro.
    ///
    /// Las escuelas viajan como claims y se recalculan en cada petición (el validador del sello de
    /// seguridad corre con intervalo 0, ver Program.cs): quitar una escuela aplica de inmediato.
    /// </summary>
    public class AlcanceEscuela
    {
        public const string ClaimPlantel = "eyesofter:plantel";
        public const string ClaimGlobal = "eyesofter:global";

        private readonly IHttpContextAccessor _http;
        public AlcanceEscuela(IHttpContextAccessor http) => _http = http;

        private ClaimsPrincipal? Usuario => _http.HttpContext?.User;

        public bool EsPersonal => Usuario is { } u && (u.IsInRole(Roles.Administrador) || u.IsInRole(Roles.Enfermeria));

        public bool EsGlobal => EsPersonal && Usuario!.IsInRole(Roles.Administrador) && Usuario.HasClaim(ClaimGlobal, "true");

        /// <summary>true = el filtro por escuela no aplica a esta petición.</summary>
        public bool SinRestriccion => !EsPersonal || EsGlobal;

        /// <summary>Escuelas asignadas (vacío si no tiene ninguna). Ignorado cuando <see cref="SinRestriccion"/>.</summary>
        public int[] Planteles => Usuario?.FindAll(ClaimPlantel).Select(c => int.TryParse(c.Value, out var id) ? id : 0).Where(id => id > 0).Distinct().ToArray()
                                  ?? Array.Empty<int>();

        public bool Permite(int plantelId) => SinRestriccion || Planteles.Contains(plantelId);
    }

    /// <summary>Agrega a la sesión las escuelas de la cuenta y si es administrador global.</summary>
    public class FabricaClaimsEyesofter : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        private readonly ApplicationDbContext _db;

        public FabricaClaimsEyesofter(UserManager<ApplicationUser> usuarios, RoleManager<IdentityRole> roles,
            IOptions<IdentityOptions> opciones, ApplicationDbContext db) : base(usuarios, roles, opciones)
        {
            _db = db;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identidad = await base.GenerateClaimsAsync(user);
            if (user.AdministradorGlobal) identidad.AddClaim(new Claim(AlcanceEscuela.ClaimGlobal, "true"));
            var planteles = _db.UsuarioPlanteles.Where(x => x.UsuarioId == user.Id).Select(x => x.PlantelId).ToList();
            foreach (var p in planteles) identidad.AddClaim(new Claim(AlcanceEscuela.ClaimPlantel, p.ToString()));
            return identidad;
        }
    }
}
