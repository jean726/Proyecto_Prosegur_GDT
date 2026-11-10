namespace ProsegurGDT.Web.Domain;

public record OpcionMenu(string Texto, string Controller, string Action, string[] Roles, bool Disponible);

public static class MenuCatalogo
{
    public static readonly OpcionMenu[] Items =
    {
        new("Ingreso de tarjetas", "Lotes", "Index", new[] { Roles.Supervisor }, true),
        new("Fichas de clientes", "Clientes", "Index", new[] { Roles.Supervisor, Roles.AuxiliarTelefonista }, true),
        new("Citas con clientes", "Citas", "Index", new[] { Roles.AuxiliarTelefonista }, true),
        new("Hojas de ruta", "HojasRuta", "Index", new[] { Roles.Supervisor }, false),
        new("Descargo de tarjetas", "Descargo", "Index", new[] { Roles.Descargador }, false),
        new("Clasificación por zona", "Clasificacion", "Index", new[] { Roles.Clasificador }, true),
        new("Planilla de armado", "Planillas", "Index", new[] { Roles.Clasificador }, true),
        new("Reporte de densidad", "Densidad", "Index", new[] { Roles.Clasificador }, true),
        new("Conciliación de tarjetas", "Conciliacion", "Index", new[] { Roles.Clasificador }, true),
        new("Direcciones fraudulentas", "DireccionesFraudulentas", "Index", new[] { Roles.Buscador, Roles.Clasificador, Roles.AnalistaDeRutas }, true),
        new("Tarjetas faltantes", "Faltantes", "Index", new[] { Roles.Buscador }, true),
        new("Alertas de direcciones", "DireccionesFraudulentas", "Alertas", new[] { Roles.Buscador }, true),
        new("Hoja de asignación", "Asignaciones", "Index", new[] { Roles.AnalistaDeRutas }, true),
        new("Usuarios", "Usuarios", "Index", new[] { Roles.Administrador }, true),
        new("Auditoría", "Auditoria", "Index", new[] { Roles.Administrador }, true),
    };
}