using System.Text;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Tests.Unidad;

/// <summary>Textos que ve el usuario (roles y estados en español) y lectura del CSV de UNIBANCA (CU02).</summary>
public class TextosYCsvTests
{
    [Theory]
    [InlineData(Roles.AnalistaDeRutas, "Analista de Rutas")]
    [InlineData(Roles.EncargadoDeBoveda, "Encargado de Bóveda")]
    [InlineData(Roles.AuxiliarTelefonista, "Auxiliar Telefonista")]
    [InlineData(Roles.Supervisor, "Supervisor")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void La_etiqueta_del_rol_separa_palabras_y_pone_tildes(string? rol, string esperado) =>
        Assert.Equal(esperado, Roles.Etiqueta(rol));

    [Fact]
    public void Hay_diez_roles_y_no_se_repiten()
    {
        Assert.Equal(10, Roles.Todos.Length);
        Assert.Equal(Roles.Todos.Length, Roles.Todos.Distinct().Count());
        Assert.Contains(Roles.Repartidor, Roles.Todos);
    }

    [Theory]
    [InlineData(EstadoTarjeta.IngresadaEnCustodia, "Ingresada en custodia")]
    [InlineData(EstadoTarjeta.ExtraviadaConIndiciosDeFraude, "Extraviada con indicios de fraude")]
    [InlineData(EstadoTarjeta.PendienteDeAsignacion, "Pendiente de asignación")]
    [InlineData(EstadoTarjeta.Entregada, "Entregada")]
    public void El_texto_del_estado_de_la_tarjeta_va_en_minusculas_salvo_la_primera(EstadoTarjeta e, string esperado) =>
        Assert.Equal(esperado, e.Texto());

    [Theory]
    [InlineData(EstadoHojaRuta.PendienteDeAprobacion, "Pendiente de aprobación")]
    [InlineData(EstadoHojaRuta.PendienteDeCorreccion, "Pendiente de corrección")]
    [InlineData(EstadoDescargoRuta.PendienteDeDocumentacion, "Pendiente de documentación")]
    [InlineData(VerificacionDescargo.DerivadaAlBuscador, "Derivada al Buscador")]
    [InlineData(EstadoActaFraude.IngresadaABoveda, "Ingresada a bóveda")]
    [InlineData(OrigenFraude.ReporteDelVerificador, "Reporte del Verificador")]
    public void El_texto_de_un_enum_lleva_tildes_y_mayuscula_en_los_roles(Enum valor, string esperado) =>
        Assert.Equal(esperado, valor.Texto());

    [Theory]
    [InlineData(ResultadoLlamada.NumeroIncorrecto, "Número incorrecto")]
    [InlineData(ResultadoLlamada.NumeroFueraDeServicio, "Número fuera de servicio")]
    [InlineData(ResultadoLlamada.BuzonDeVoz, "Buzón de voz")]
    public void El_resultado_de_la_llamada_lleva_tildes(ResultadoLlamada r, string esperado) =>
        Assert.Equal(esperado, r.Texto());

    [Fact]
    public void Ningun_enum_del_dominio_muestra_una_palabra_sin_su_tilde()
    {
        var enums = typeof(EstadoTarjeta).Assembly.GetTypes()
            .Where(t => t.IsEnum && t.Namespace == typeof(EstadoTarjeta).Namespace);
        var sinTilde = new System.Text.RegularExpressions.Regex(@"\b(\w+[cs]ion|numero|buzon|boveda)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var malos = (from t in enums
                     from Enum v in Enum.GetValues(t)
                     let texto = v.Texto()
                     where sinTilde.IsMatch(texto)
                     select $"{t.Name}.{v} → «{texto}»").ToList();

        Assert.True(malos.Count == 0, "Textos sin tilde: " + string.Join("; ", malos));
    }

    [Fact]
    public void Ningun_estado_de_tarjeta_queda_sin_texto_ni_pegado()
    {
        foreach (var e in Enum.GetValues<EstadoTarjeta>())
        {
            var texto = e.Texto();
            Assert.False(string.IsNullOrWhiteSpace(texto));
            Assert.True(char.IsUpper(texto[0]), texto);
            if (e.ToString().Count(char.IsUpper) > 1) Assert.Contains(' ', texto);
        }
    }

    [Theory]
    [InlineData(ResultadoLlamada.NoContesta, true)]
    [InlineData(ResultadoLlamada.BuzonDeVoz, true)]
    [InlineData(ResultadoLlamada.NumeroIncorrecto, true)]
    [InlineData(ResultadoLlamada.NumeroFueraDeServicio, true)]
    [InlineData(ResultadoLlamada.Otro, true)]
    [InlineData(ResultadoLlamada.Exitoso, false)]
    [InlineData(ResultadoLlamada.ContactadoSinAcuerdo, false)]
    [InlineData(ResultadoLlamada.DatosInsuficientes, false)]
    public void Solo_los_resultados_sin_contacto_cuentan_como_intento_fallido(ResultadoLlamada r, bool esperado) =>
        Assert.Equal(esperado, r.EsNoContacto());

    // ---------- CSV de UNIBANCA ----------
    private static Task<LecturaFicha> Leer(string csv) =>
        new AdaptadorCsvUnibanca().LeerAsync(new MemoryStream(Encoding.UTF8.GetBytes(csv)));

    private const string Cabecera = "CodigoTarjeta;NombreCompleto;Documento;Telefono;Direccion\n";

    [Fact]
    public async Task Lee_un_csv_con_punto_y_coma()
    {
        var r = await Leer(Cabecera + "TJ-001;Ana Torres;45871236;987654321;Av. Larco 123\nTJ-002;Luis Rojas;41235678;;Jr. Unión 450\n");

        Assert.Empty(r.Errores);
        Assert.Equal(2, r.Filas.Count);
        Assert.Equal(new FilaFicha(2, "TJ-001", "Ana Torres", "45871236", "987654321", "Av. Larco 123"), r.Filas[0]);
        Assert.Equal("", r.Filas[1].Telefono);
    }

    [Fact]
    public async Task Lee_un_csv_con_comas_comillas_y_columnas_en_otro_orden()
    {
        var r = await Leer("Direccion,Documento,NombreCompleto,Telefono,CodigoTarjeta\n\"Av. Larco 123, Dpto. \"\"B\"\", Miraflores\",45871236,\"Torres, Ana\",987654321,TJ-001\n");

        var f = Assert.Single(r.Filas);
        Assert.Empty(r.Errores);
        Assert.Equal("Av. Larco 123, Dpto. \"B\", Miraflores", f.Direccion);
        Assert.Equal("Torres, Ana", f.Nombre);
        Assert.Equal("TJ-001", f.Codigo);
    }

    [Fact]
    public async Task Acepta_el_archivo_con_marca_BOM_y_lineas_en_blanco()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Cabecera + "\n\nTJ-001;Ana Torres;45871236;;Av. Larco 123\n\n")).ToArray();
        var r = await new AdaptadorCsvUnibanca().LeerAsync(new MemoryStream(bytes));

        Assert.Empty(r.Errores);
        Assert.Single(r.Filas);
    }

    [Fact]
    public async Task Un_archivo_sin_registros_da_error()
    {
        var r = await Leer(Cabecera);
        Assert.Empty(r.Filas);
        Assert.Equal("El archivo no contiene registros.", Assert.Single(r.Errores));
    }

    [Fact]
    public async Task Si_faltan_columnas_las_nombra()
    {
        var r = await Leer("CodigoTarjeta;NombreCompleto\nTJ-001;Ana\n");

        Assert.Empty(r.Filas);
        var error = Assert.Single(r.Errores);
        Assert.Contains("documento", error);
        Assert.Contains("telefono", error);
        Assert.Contains("direccion", error);
    }

    [Fact]
    public async Task Marca_la_linea_de_cada_dato_invalido_y_los_codigos_duplicados()
    {
        var r = await Leer(Cabecera
            + "TJ-001;Ana Torres;45871236;;Av. Larco 123\n"
            + "TJ-001;Luis Rojas;41235678;;Jr. Unión 450\n"                         // línea 3: duplicado
            + ";Sin Código;40000001;;Calle 1\n"                                     // línea 4: sin código
            + "TJ-004;;40000002;;Calle 2\n"                                         // línea 5: sin nombre
            + "TJ-005;Sin Doc;;;Calle 3\n"                                          // línea 6: sin documento
            + "TJ-006;Sin Dir;40000003;;\n"                                         // línea 7: sin dirección
            + $"TJ-007;Tel Largo;40000004;{new string('9', 21)};Calle 4\n");        // línea 8: teléfono largo

        Assert.Equal(7, r.Filas.Count);
        Assert.Equal(6, r.Errores.Count);
        Assert.Contains(r.Errores, e => e.StartsWith("Línea 3:") && e.Contains("duplicado"));
        Assert.Contains(r.Errores, e => e.StartsWith("Línea 4:") && e.Contains("código"));
        Assert.Contains(r.Errores, e => e.StartsWith("Línea 5:") && e.Contains("nombre"));
        Assert.Contains(r.Errores, e => e.StartsWith("Línea 6:") && e.Contains("documento"));
        Assert.Contains(r.Errores, e => e.StartsWith("Línea 7:") && e.Contains("dirección"));
        Assert.Contains(r.Errores, e => e.StartsWith("Línea 8:") && e.Contains("teléfono"));
    }

    [Fact]
    public async Task Un_binario_renombrado_a_csv_se_rechaza()
    {
        byte[] zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00, 0x0A, 0x41, 0x3B, 0x42, 0x0A];

        var r = await new AdaptadorCsvUnibanca().LeerAsync(new MemoryStream(zip));

        Assert.Empty(r.Filas);
        Assert.Contains("no es un CSV de texto válido", Assert.Single(r.Errores));
    }

    [Fact]
    public async Task Un_csv_guardado_en_otra_codificacion_se_rechaza_en_lugar_de_importar_nombres_rotos()
    {
        var latin1 = Encoding.Latin1.GetBytes(Cabecera + "TJ-001;José Núñez;45871236;;Av. Larco 123\n");

        var r = await new AdaptadorCsvUnibanca().LeerAsync(new MemoryStream(latin1));

        Assert.Empty(r.Filas);
        Assert.Contains("UTF-8", Assert.Single(r.Errores));
    }

    [Fact]
    public async Task Las_tabulaciones_dentro_de_un_campo_no_cuentan_como_binario()
    {
        var r = await Leer(Cabecera + "TJ-001;Ana\tTorres;45871236;;Av. Larco 123\n");
        Assert.Empty(r.Errores);
        Assert.Single(r.Filas);
    }

    [Theory]
    [InlineData("ficha.csv", true)]
    [InlineData("FICHA.CSV", true)]
    [InlineData("ficha.xlsx", false)]
    [InlineData("ficha.csv.exe", false)]
    public void Solo_admite_archivos_csv(string nombre, bool esperado) =>
        Assert.Equal(esperado, new AdaptadorCsvUnibanca().Soporta(nombre));
}
