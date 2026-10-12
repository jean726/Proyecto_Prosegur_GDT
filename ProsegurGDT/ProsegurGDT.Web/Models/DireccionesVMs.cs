using System.ComponentModel.DataAnnotations;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Services;
namespace ProsegurGDT.Web.Models;

public class DireccionCrearVM
{
    [Required, MaxLength(250), Display(Name = "Dirección")] public string Direccion { get; set; } = "";
    [Required, MaxLength(300), Display(Name = "Motivo / fundamento")] public string Motivo { get; set; } = "";
}

public class DireccionesIndexVM
{
    public List<DireccionFraudulenta> Items { get; set; } = new();
    public string? Probar { get; set; }
    public List<CoincidenciaDireccion>? Coincidencias { get; set; }
    public int AlertasPendientes { get; set; }
    public double UmbralMedio { get; set; }
    public double UmbralAlto { get; set; }
}