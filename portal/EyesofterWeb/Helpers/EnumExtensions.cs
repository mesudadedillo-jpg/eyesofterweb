using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace EyesofterWeb.Helpers
{
    public static class EnumExtensions
    {
        /// <summary>Devuelve el texto de [Display(Name = ...)] de un valor de enum, o su nombre.</summary>
        public static string Texto(this Enum valor)
        {
            var miembro = valor.GetType().GetMember(valor.ToString()).FirstOrDefault();
            return miembro?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? valor.ToString();
        }
    }
}
