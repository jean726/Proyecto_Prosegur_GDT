using System.Text.RegularExpressions;
namespace ProsegurGDT.Web.Domain;

public static class EnumExt
{
    public static string Texto(this Enum valor) => Regex.Replace(valor.ToString(), "(?<!^)([A-Z])", " $1");
}