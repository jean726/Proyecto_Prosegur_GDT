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

    public static readonly string[] Todos =
    {
        Administrador, Supervisor, AuxiliarTelefonista, Verificador, Descargador,
        EncargadoDeBoveda, Clasificador, Buscador, AnalistaDeRutas
    };
}