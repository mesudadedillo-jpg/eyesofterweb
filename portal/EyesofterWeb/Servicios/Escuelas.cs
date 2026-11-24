using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Seguridad;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Servicios
{
    /// <summary>Las escuelas que puede elegir quien hace la petición (en formularios y filtros).</summary>
    public class Escuelas
    {
        private readonly ApplicationDbContext _db;
        private readonly AlcanceEscuela _alcance;

        public Escuelas(ApplicationDbContext db, AlcanceEscuela alcance)
        {
            _db = db;
            _alcance = alcance;
        }

        /// <summary>Escuelas activas visibles: todas para el administrador global, las asignadas para el resto del personal.</summary>
        public async Task<List<Plantel>> VisiblesAsync()
        {
            var q = _db.Planteles.AsNoTracking().Where(p => p.Activo);
            if (!_alcance.SinRestriccion)
            {
                var ids = _alcance.Planteles;
                q = q.Where(p => ids.Contains(p.Id));
            }
            return await q.OrderBy(p => p.Nombre).ToListAsync();
        }

        public async Task<SelectList> ListaAsync(int? seleccionado = null)
        {
            var lista = await VisiblesAsync();
            return new SelectList(lista, nameof(Plantel.Id), nameof(Plantel.Nombre), seleccionado ?? lista.FirstOrDefault()?.Id);
        }

        /// <summary>La escuela que se propone en un formulario nuevo: la única, o la primera.</summary>
        public async Task<int?> PorDefectoAsync() => (await VisiblesAsync()).FirstOrDefault()?.Id;

        /// <summary>¿Puede guardar algo en esa escuela? (existe, está activa y la tiene asignada).</summary>
        public async Task<bool> PuedeUsarAsync(int plantelId) =>
            _alcance.Permite(plantelId) && await _db.Planteles.AnyAsync(p => p.Id == plantelId && p.Activo);
    }
}
