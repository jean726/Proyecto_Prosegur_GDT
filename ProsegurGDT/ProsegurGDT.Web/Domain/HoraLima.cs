namespace ProsegurGDT.Web.Domain;

public static class HoraLima
{
    private static readonly TimeZoneInfo Zona = Buscar();

    private static TimeZoneInfo Buscar()
    {
        foreach (var id in new[] { "America/Lima", "SA Pacific Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    public static DateTime Convertir(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona);

    public static DateTime Ahora => Convertir(DateTime.UtcNow);
}