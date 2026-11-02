namespace ProsegurGDT.Web.Domain;

public static class ResultadoLlamadaExt
{
    private static readonly ResultadoLlamada[] NoContacto =
    {
        ResultadoLlamada.NoContesta, ResultadoLlamada.BuzonDeVoz, ResultadoLlamada.NumeroIncorrecto,
        ResultadoLlamada.NumeroFueraDeServicio, ResultadoLlamada.Otro
    };

    public static bool EsNoContacto(this ResultadoLlamada r) => NoContacto.Contains(r);
}