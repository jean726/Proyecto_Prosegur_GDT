namespace ProsegurGDT.Web.Models
{
    // C4 — Página de estado (404, 400, 500…) con el estilo de la plataforma y en español.
    public class EstadoViewModel
    {
        public int Codigo { get; set; }
        public string Titulo { get; set; } = "";
        public string Mensaje { get; set; } = "";
        // Identificador de la solicitud: solo en errores del servidor, para localizarla en el registro
        public string? Seguimiento { get; set; }
    }
}
