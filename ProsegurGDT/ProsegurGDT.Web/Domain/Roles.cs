namespace ProsegurGDT.Web.Domain;

public static class Roles
{
    public const string Administrador = "Administrador";
    public const string Supervisor = "Supervisor";
    public const string AuxiliarTelefonista = "AuxiliarTelefonista";
    public const string Verificador = "Verificador";
    public const string Descargador = "Descargador";
    public const string EncargadoDeBoveda = "EncargadoDeBoveda";
    public const string Clasificador = "Clasificador";
    public const string Buscador = "Buscador";
    public const string AnalistaDeRutas = "AnalistaDeRutas";
    // B2: solo opera por la API móvil (/api/v1); la web le rechaza el inicio de sesión (RN-29)
    public const string Repartidor = "Repartidor";

    /// <summary>Nombre del rol para mostrar: «AnalistaDeRutas» → «Analista de Rutas».</summary>
    public static string Etiqueta(string? rol)
    {
        if (string.IsNullOrEmpty(rol)) return "";
        var palabras = System.Text.RegularExpressions.Regex.Split(rol, "(?<!^)(?=[A-Z])")
            .Select(w => w == "De" ? "de" : w == "Boveda" ? "Bóveda" : w);
        return string.Join(' ', palabras);
    }

    public static readonly string[] Todos =
    {
        Administrador, Supervisor, AuxiliarTelefonista, Verificador, Descargador,
        EncargadoDeBoveda, Clasificador, Buscador, AnalistaDeRutas, Repartidor
    };
}