namespace ProsegurGDT.Web.Domain;

public record OpcionMenu(string Texto, string Controller, string Action, string[] Roles, bool Disponible);

public static class MenuCatalogo
{
    public static readonly OpcionMenu[] Items =
    {
        new("Ingreso de tarjetas", "Lotes", "Index", new[] { Roles.Supervisor }, true),
        new("Fichas de clientes", "Clientes", "Index", new[] { Roles.Supervisor, Roles.AuxiliarTelefonista }, false),
        new("Citas con clientes", "Citas", "Index", new[] { Roles.AuxiliarTelefonista }, false),
        new("Hojas de ruta", "HojasRuta", "Index", new[] { Roles.Supervisor }, false),
        new("Descargo de tarjetas", "Descargo", "Index", new[] { Roles.Descargador }, false),
        new("Clasificación por zona", "Clasificacion", "Index", new[] { Roles.Clasificador }, false),
        new("Direcciones fraudulentas", "DireccionesFraudulentas", "Index", new[] { Roles.Buscador }, false),
        new("Hoja de asignación", "Asignaciones", "Index", new[] { Roles.AnalistaDeRutas }, false),
        new("Usuarios", "Usuarios", "Index", new[] { Roles.Administrador }, true),
        new("Auditoría", "Auditoria", "Index", new[] { Roles.Administrador }, true),
    };
}