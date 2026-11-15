using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Data;

// Lote de demostración "limpio": sin direcciones fraudulentas y ya procesado hasta las hojas de ruta,
// para que cada rol vea contenido sin recorrer todo el flujo. Se construye con los mismos servicios
// que usan las pantallas, de modo que los estados y los vínculos son los reales.
// Cada etapa comprueba si ya existe, así que se puede repetir sin duplicar datos.
public static class DbSeederLoteDemo
{
    private const string NumeroRemito = "REM-DEMO-01";

    // Zona, cantidad de tarjetas con cita, tipo de repartidor requerido, repartidor
    private static readonly (string Zona, int Cantidad, TipoRepartidorRequerido Tipo, string Repartidor)[] Rutas =
    {
        ("Miraflores", 6, TipoRepartidorRequerido.Motorizado, "Carlos Mendoza"),
        ("San Isidro", 5, TipoRepartidorRequerido.Motorizado, "Luis Ramírez"),
        ("Santiago de Surco", 5, TipoRepartidorRequerido.Motorizado, "Jorge Salazar"),
        ("San Borja", 3, TipoRepartidorRequerido.Caminante, "Ana Torres"),
        ("Barranco", 2, TipoRepartidorRequerido.Caminante, "Miguel Castro"),
        ("Lince", 3, TipoRepartidorRequerido.Caminante, "Rosa Quispe"),
    };

    private const int PendientesDeContacto = 3;   // zona Surquillo, para el Auxiliar Telefonista
    private const int IntentosAgotados = 2;       // sin zona, no clasificables

    private static readonly Dictionary<string, string[]> Calles = new()
    {
        ["Miraflores"] = new[] { "Calle Schell", "Av. Pardo", "Calle Alcanfores", "Av. Larco", "Calle Tarata", "Calle Esquilache" },
        ["San Isidro"] = new[] { "Av. Javier Prado Oeste", "Calle Los Cedros", "Av. Camino Real", "Calle Las Begonias", "Av. Pezet" },
        ["Santiago de Surco"] = new[] { "Av. Caminos del Inca", "Calle Monte Rosa", "Av. Primavera", "Calle Los Tulipanes", "Av. Tomás Marsano" },
        ["San Borja"] = new[] { "Av. San Luis", "Calle Las Dalias", "Av. Aviación" },
        ["Barranco"] = new[] { "Av. Grau", "Calle Domeyer" },
        ["Lince"] = new[] { "Av. Arequipa", "Calle Manuel Candamo", "Av. Petit Thouars" },
        ["Surquillo"] = new[] { "Av. Angamos Este", "Calle Dante", "Av. Tomás Marsano" },
        ["Jesús María"] = new[] { "Av. Cuba", "Calle Castilla" },
    };

    private static readonly string[] Nombres =
    {
        "Mariana", "Sebastián", "Valeria", "Diego", "Camila", "Andrés", "Lucía", "Fernando", "Daniela", "Ricardo",
        "Paola", "Javier", "Natalia", "Renato", "Alejandra", "Gustavo", "Carolina", "Marco", "Silvana", "Hugo"
    };

    private static readonly string[] Apellidos =
    {
        "Vega Paredes", "Rojas Cárdenas", "Huamán Salas", "Núñez Ibarra", "Soto Maldonado", "Peña Villanueva", "Chávez Lozano",
        "Aguilar Benites", "Ramos Delgado", "Medina Cabrera", "Ortiz Valdivia", "Campos Arce", "Paz Sandoval", "Torres Rivas",
        "León Zegarra", "Cruz Montalvo", "Salazar Pinto", "Herrera Bravo", "Flores Cuba", "Mendoza Ayala"
    };

    public static async Task SembrarAsync(IServiceProvider sp, ILogger logger)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var um = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var supervisor = await um.FindByEmailAsync($"{Roles.Supervisor.ToLowerInvariant()}@gdt.local");
        var telefonista = await um.FindByEmailAsync($"{Roles.AuxiliarTelefonista.ToLowerInvariant()}@gdt.local");
        var clasificador = await um.FindByEmailAsync($"{Roles.Clasificador.ToLowerInvariant()}@gdt.local");
        var analista = await um.FindByEmailAsync($"{Roles.AnalistaDeRutas.ToLowerInvariant()}@gdt.local");
        if (supervisor is null || telefonista is null || clasificador is null || analista is null) return;

        var zonas = await db.Zonas.ToDictionaryAsync(z => z.Nombre, z => z.Id);
        var banco = await db.Bancos.OrderBy(b => b.Id).FirstOrDefaultAsync();
        if (banco is null || Rutas.Any(r => !zonas.ContainsKey(r.Zona)) || !zonas.ContainsKey("Surquillo")) return;

        var lote = await CrearLoteAsync(sp, db, banco, zonas, supervisor, telefonista);
        if (lote is null) return;

        var planillas = sp.GetRequiredService<IPlanillaArmadoService>();
        var densidad = sp.GetRequiredService<IDensidadService>();
        var asignaciones = sp.GetRequiredService<IAsignacionService>();
        var hojasRuta = sp.GetRequiredService<IHojaRutaService>();

        // Clasificador: planilla de armado y reporte de densidad
        if (!await db.Planillas.AnyAsync(p => p.LoteId == lote.Id))
        {
            var vm = await planillas.CargarAsync(lote.Id);
            if (vm is null) return;
            var tipos = Rutas.ToDictionary(r => zonas[r.Zona], r => r.Tipo);
            foreach (var z in vm.Zonas)
                z.TipoRepartidorRequerido = tipos.GetValueOrDefault(z.ZonaId, TipoRepartidorRequerido.Indiferente);
            var rp = await planillas.GuardarAsync(vm, clasificador.Id);
            if (!rp.Ok) { logger.LogWarning("Lote demo: no se pudo armar la planilla: {Errores}", string.Join(" ", rp.Errores)); return; }
            var planilla = await db.Planillas.FirstAsync(p => p.LoteId == lote.Id);
            await AuditarAsync(db, clasificador, "GenerarPlanillaArmado", "Planilla", planilla.Id.ToString(), $"Lote {lote.CodigoHojaControl} (demo)");
        }

        if (!await db.ReportesDensidad.AnyAsync(r => r.LoteId == lote.Id))
        {
            var rd = await densidad.GenerarAsync(lote.Id, clasificador.Id);
            if (!rd.Ok) { logger.LogWarning("Lote demo: no se pudo generar el reporte de densidad: {Error}", rd.Error); return; }
            await AuditarAsync(db, clasificador, "GenerarReporteDensidad", "ReporteDensidad", rd.ReporteId.ToString(), rd.Detalle);
        }

        // Analista de Rutas: hoja de asignación con un repartidor por zona
        var hoja = await db.HojasAsignacion.FirstOrDefaultAsync(h => h.LoteId == lote.Id);
        if (hoja is null)
        {
            var repartidores = await db.Repartidores.ToDictionaryAsync(r => r.Nombre, r => r.Id);
            if (Rutas.Any(r => !repartidores.ContainsKey(r.Repartidor))) return;

            var vm = await asignaciones.CargarAsync(lote.Id);
            if (vm is null) return;
            var porZona = Rutas.ToDictionary(r => zonas[r.Zona], r => repartidores[r.Repartidor]);
            foreach (var z in vm.Zonas)
            {
                z.Accion = AccionZonaAsignacion.Asignar;
                z.RepartidorId = porZona.TryGetValue(z.ZonaId, out var rid) ? rid : null;
            }
            vm.ConfirmarAdvertencias = true;
            var ra = await asignaciones.GenerarAsync(vm, analista.Id);
            if (!ra.Ok) { logger.LogWarning("Lote demo: no se pudo generar la hoja de asignación: {Errores}", string.Join(" ", ra.Errores)); return; }
            hoja = await db.HojasAsignacion.FirstAsync(h => h.LoteId == lote.Id);
            await AuditarAsync(db, analista, "GenerarHojaAsignacion", "HojaAsignacion", hoja.Id.ToString(), ra.Detalle);
        }

        // Supervisor: hojas de ruta pendientes de aprobación
        if (!await db.HojasRuta.AnyAsync(h => h.AsignacionRuta!.HojaAsignacionId == hoja.Id))
        {
            var rh = await hojasRuta.GenerarAsync(hoja.Id, supervisor.Id);
            if (!rh.Ok) logger.LogWarning("Lote demo: no se pudieron generar las hojas de ruta: {Error}", rh.Mensaje);
            else await AuditarAsync(db, supervisor, "GenerarHojasRuta", "HojaAsignacion", hoja.Id.ToString(), rh.Detalle);
        }
    }

    private static async Task<Lote?> CrearLoteAsync(IServiceProvider sp, ApplicationDbContext db, Banco banco,
        Dictionary<string, int> zonas, ApplicationUser supervisor, ApplicationUser telefonista)
    {
        var existente = await db.Lotes.FirstOrDefaultAsync(l => l.Remito!.Numero == NumeroRemito);
        if (existente is not null) return existente;

        var estados = sp.GetRequiredService<ITarjetaEstadoService>();
        var total = Rutas.Sum(r => r.Cantidad) + PendientesDeContacto + IntentosAgotados;

        var remito = await db.Remitos.FirstOrDefaultAsync(r => r.Numero == NumeroRemito)
            ?? new Remito { Numero = NumeroRemito, BancoId = banco.Id, CantidadDeclarada = total, FechaEmision = DateOnly.FromDateTime(HoraLima.Ahora) };
        if (remito.Id == 0) db.Remitos.Add(remito);

        var ahora = DateTime.UtcNow;
        var hoy = DateOnly.FromDateTime(HoraLima.Ahora);
        var lote = new Lote
        {
            Remito = remito,
            FechaRecepcion = hoy,
            CantidadDeclarada = total,
            CantidadContada = total,
            Estado = EstadoLote.IngresadoEnCustodia,
            RegistradoPorId = supervisor.Id,
            FechaHoraRegistro = ahora
        };
        db.Lotes.Add(lote);
        await db.SaveChangesAsync();
        lote.CodigoHojaControl = $"HC-{HoraLima.Convertir(ahora):yyyyMMdd}-{lote.Id:D5}";

        var n = 0;
        Tarjeta Nueva(string zona, string[] calles, int k, int? zonaId)
        {
            n++;
            var cliente = new Cliente
            {
                NombreCompleto = $"{Nombres[n % Nombres.Length]} {Apellidos[(n * 7) % Apellidos.Length]}",
                DocumentoIdentidad = $"9900{n:D4}",
                Telefono = $"9555{n:D5}",
                Direccion = $"{calles[k % calles.Length]} {100 + n * 13}, {zona}"
            };
            cliente.DireccionNormalizada = NormalizadorDirecciones.Normalizar(cliente.Direccion);
            var t = new Tarjeta
            {
                Codigo = $"DEMO-{lote.Id:D3}-{n:D4}",
                Lote = lote,
                Cliente = cliente,
                Estado = EstadoTarjeta.IngresadaEnCustodia,
                FechaActualizacion = ahora,
                ZonaId = zonaId
            };
            db.Tarjetas.Add(t);
            db.HistorialEstados.Add(new HistorialEstadoTarjeta
            {
                Tarjeta = t, EstadoAnterior = null, EstadoNuevo = EstadoTarjeta.IngresadaEnCustodia,
                UsuarioId = supervisor.Id, FechaHora = ahora, Observacion = $"Ingreso del lote {lote.CodigoHojaControl}"
            });
            return t;
        }

        // Tarjetas con cita programada (las que entran a rutas)
        foreach (var (zona, cantidad, _, _) in Rutas)
            for (var i = 0; i < cantidad; i++)
            {
                var t = Nueva(zona, Calles[zona], i, zonas[zona]);
                db.Llamadas.Add(new RegistroLlamada
                {
                    Tarjeta = t, Resultado = ResultadoLlamada.Exitoso, Observaciones = "Cliente confirma la entrega en su domicilio.",
                    UsuarioId = telefonista.Id, FechaHora = ahora
                });
                db.Citas.Add(new Cita
                {
                    Tarjeta = t, FechaEntrega = hoy, HoraEntrega = new TimeOnly(9 + i % 8, i % 2 == 0 ? 0 : 30),
                    Lugar = t.Cliente!.Direccion, Vigente = true, RegistradaPorId = telefonista.Id, FechaHoraRegistro = ahora
                });
                estados.Cambiar(t, EstadoTarjeta.CitaProgramada, telefonista.Id, "Cita programada con el cliente");
            }

        // Tarjetas por contactar (clasificadas en Surquillo)
        for (var i = 0; i < PendientesDeContacto; i++)
        {
            var t = Nueva("Surquillo", Calles["Surquillo"], i, zonas["Surquillo"]);
            db.Llamadas.Add(new RegistroLlamada
            {
                Tarjeta = t, Resultado = ResultadoLlamada.NoContesta, Observaciones = "No contesta; reintentar más tarde.",
                UsuarioId = telefonista.Id, FechaHora = ahora
            });
            estados.Cambiar(t, EstadoTarjeta.PendienteDeContacto, telefonista.Id, "Primer intento sin contacto");
        }

        // Tarjetas con los intentos de contacto agotados
        for (var i = 0; i < IntentosAgotados; i++)
        {
            var t = Nueva("Jesús María", Calles["Jesús María"], i, null);
            for (var c = 0; c < 3; c++)
                db.Llamadas.Add(new RegistroLlamada
                {
                    Tarjeta = t, Resultado = ResultadoLlamada.NoContesta, Observaciones = $"Intento {c + 1} sin respuesta.",
                    UsuarioId = telefonista.Id, FechaHora = ahora
                });
            estados.Cambiar(t, EstadoTarjeta.IntentosAgotados, telefonista.Id, "Se agotaron los intentos de contacto");
        }

        await db.SaveChangesAsync();

        // Mismo rastro que deja el flujo real (LotesController / ClientesController)
        await AuditarAsync(db, supervisor, "RegistrarLote", "Lote", lote.Id.ToString(),
            $"Remito {NumeroRemito}: declaradas {total}, contadas {total}, ingresado en custodia (demo)");
        await AuditarAsync(db, supervisor, "GenerarFichaClientes", "Lote", lote.Id.ToString(), $"{total} tarjetas (demo)");
        return lote;
    }

    // El servicio de auditoría toma el usuario de la sesión HTTP; el sembrado no tiene sesión, así que se registra aquí.
    private static async Task AuditarAsync(ApplicationDbContext db, ApplicationUser usuario, string accion, string entidad, string entidadId, string? detalle)
    {
        db.AuditLogs.Add(new AuditLog
        {
            FechaHoraUtc = DateTime.UtcNow,
            UsuarioId = usuario.Id,
            UsuarioNombre = usuario.UserName ?? usuario.Email ?? "sistema",
            Accion = accion, Entidad = entidad, EntidadId = entidadId,
            Detalle = detalle is { Length: > 500 } ? detalle[..500] : detalle
        });
        await db.SaveChangesAsync();
    }
}
