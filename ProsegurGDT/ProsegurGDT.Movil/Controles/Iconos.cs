namespace ProsegurGDT.Movil.Controles;

// Iconos lineales del diseño (cuadrícula de 24 × 24, trazo redondeado). Se dibujan con <c:Icono Datos="…" />.
// Las órdenes van explícitas (sin abreviaturas SVG) para el conversor de geometría de MAUI.
public static class Iconos
{
    private const string Circulo = "M3,12 A9,9 0 1 1 21,12 A9,9 0 1 1 3,12 Z";

    public const string Ojo = "M2,12 C2,12 6,5 12,5 C18,5 22,12 22,12 C22,12 18,19 12,19 C6,19 2,12 2,12 Z M9,12 A3,3 0 1 1 15,12 A3,3 0 1 1 9,12 Z";
    public const string OjoTachado = Ojo + " M3,3 L21,21";
    public const string Alerta = "M12,3 L22,21 L2,21 Z M12,10 L12,15 M12,18 L12,18.5";
    public const string Candado = "M7,11 L17,11 A2,2 0 0 1 19,13 L19,19 A2,2 0 0 1 17,21 L7,21 A2,2 0 0 1 5,19 L5,13 A2,2 0 0 1 7,11 Z M8,11 L8,7 A4,4 0 0 1 16,7 L16,11";
    public const string Salir = "M15,4 L19,4 L19,20 L15,20 M10,8 L6,12 L10,16 M6,12 L16,12";
    public const string Actualizar = "M20,12 A8,8 0 1 1 17.7,6.3 M20,4 L20,9 L15,9";
    public const string Documento = "M7,3 L17,3 A2,2 0 0 1 19,5 L19,19 A2,2 0 0 1 17,21 L7,21 A2,2 0 0 1 5,19 L5,5 A2,2 0 0 1 7,3 Z M9,8 L15,8 M9,12 L15,12 M9,16 L12,16";
    public const string FlechaAbajo = "M12,5 L12,19 M6,13 L12,19 L18,13";
    public const string Atras = "M15,5 L8,12 L15,19";
    public const string Siguiente = "M9,5 L16,12 L9,19";
    public const string Info = Circulo + " M12,11 L12,17 M12,7.5 L12,8";
    public const string ErrorCirculo = Circulo + " M12,8 L12,13 M12,16 L12,16.5";
    public const string Check = "M5,12 L10,17 L19,7";
    public const string Telefono = "M5,4 L9,4 L11,9 L8.5,10.5 A11,11 0 0 0 13.5,15.5 L15,13 L20,15 L20,19 A2,2 0 0 1 18,21 A16,16 0 0 1 3,6 A2,2 0 0 1 5,4 Z";
    public const string Escudo = "M12,3 L20,6 L20,12 C20,17 16.5,20 12,21 C7.5,20 4,17 4,12 L4,6 Z";
    public const string EscudoAlerta = Escudo + " M12,9 L12,13 M12,16 L12,16.5";
    public const string Cerrar = "M18,6 L6,18 M6,6 L18,18";
    public const string SinSenal = "M2,8.5 A15,15 0 0 1 7,5.5 M5,12 A10,10 0 0 1 8.5,9.8 M8.5,15.5 A5,5 0 0 1 15.5,15.5 M12,19 L12,19.5 M3,3 L21,21 M15,6 A15,15 0 0 1 22,8.5 M15.5,10.5 A10,10 0 0 1 19,12";
    public const string Reloj = Circulo + " M12,7 L12,12 L15,14";
    // Glifo propio de la app (tarjeta), el mismo del icono y del splash.
    public const string Tarjeta = "M5,6 L19,6 A2,2 0 0 1 21,8 L21,17 A2,2 0 0 1 19,19 L5,19 A2,2 0 0 1 3,17 L3,8 A2,2 0 0 1 5,6 Z M3,10 L21,10 M6,15.5 L10,15.5";
}
