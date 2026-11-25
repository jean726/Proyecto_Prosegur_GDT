# Prosegur GDT — Gestión y control operativo de tarjetas

Sistema integral para el ciclo de entrega de tarjetas de crédito: ingreso en custodia, ficha de clientes, citas, clasificación por zona con alertas de direcciones fraudulentas, armado de rutas, mensajería, descargo y auditoría. Proyecto final del curso de Modelamiento y Análisis de Software.

Tiene dos frentes:

- **Plataforma web** para nueve roles de oficina (ASP.NET Core MVC).
- **Aplicación móvil** para el repartidor (Android), que consume la API REST de la web.

## Arquitectura

| Parte | Tecnología | Carpeta |
|---|---|---|
| Web y API REST `/api/v1` | ASP.NET Core 10 MVC, EF Core 10, SQL Server, ASP.NET Identity, JWT | `ProsegurGDT/ProsegurGDT.Web` |
| App móvil del repartidor | .NET MAUI (Android), MVVM | `ProsegurGDT/ProsegurGDT.Movil` |
| Pruebas automatizadas | xUnit, `WebApplicationFactory`, EF en memoria | `ProsegurGDT/ProsegurGDT.Tests` |
| Motor de alertas de direcciones | Modelo ONNX MiniLM multilingüe + n-gramas (híbrido) | `Services/*Embedding*`, `AddressRiskService` |

La web sigue una organización por capas: `Controllers` (entrada HTTP y autorización por rol), `Services` (reglas de negocio), `Domain` (entidades y enumeraciones) y `Data` (contexto de EF, migraciones y datos de demostración). Patrones usados: Strategy (resultado de llamada), Adapter (lectura del CSV del banco), Singleton (configuración operativa) y una máquina de estados para la tarjeta (`ITarjetaEstadoService`).

En producción se despliega en **Azure App Service (Linux)** con **Azure SQL**.

## Roles

Administrador, Supervisor, Auxiliar Telefonista, Clasificador, Buscador, Analista de Rutas, Verificador, Descargador y Encargado de Bóveda usan la web. El **Repartidor** usa solo la app móvil.

## Cómo arrancar la web en local

Requisitos: .NET SDK 10 y SQL Server LocalDB (viene con Visual Studio).

```bash
cd ProsegurGDT/ProsegurGDT.Web
dotnet user-secrets set "SeedAdmin:Email" "admin@gdt.local"
dotnet user-secrets set "SeedAdmin:Password" "<una contraseña>"
dotnet run --launch-profile https
```

Al arrancar aplica las migraciones y, en el entorno de desarrollo, carga datos de demostración: una cuenta por rol (`supervisor@gdt.local`, `clasificador@gdt.local`…) con la contraseña que se fije en *User Secrets* (`dotnet user-secrets set "Seed:ClaveDemo" "<una contraseña>"`) y un lote ya procesado hasta las hojas de ruta (se omite con `Seed:LoteDemo=false`). Cada repartidor tiene además una cuenta para la app móvil: `repartidor@gdt.local` (Carlos Mendoza) y `nombre.apellido@gdt.local` para los demás, con la misma contraseña.

Abra `https://localhost:7019`. El estado del servicio está en `/health`.

## Cómo ejecutar las pruebas

```bash
cd ProsegurGDT/ProsegurGDT.Tests
dotnet test
```

Son más de 450 pruebas en tres niveles:

- **Unitarias:** transiciones de los 14 estados de la tarjeta, reglas del descargo, normalizador de direcciones, motor de IA y umbrales, filtro de intranet por IP, token JWT y lector de CSV.
- **De servicio** (EF en memoria): alertas de direcciones e importación de la ficha de clientes.
- **De integración** (la aplicación real sobre SQL Server): migraciones, autorización de cada pantalla para cada rol, inicio de sesión, seguridad (cabeceras, política de contenido, cookies, límites de intentos) y la jornada completa del repartidor por la API.

Las de integración crean una base temporal `gdt-pruebas-…` en LocalDB y la borran al terminar. Para usar otro servidor: variable de entorno `GDT_TEST_SQL`.

Cada `push` ejecuta la compilación y las pruebas en GitHub Actions (`.github/workflows/ci.yml`).

## App móvil

Requisitos: carga de trabajo «Desarrollo de .NET MAUI» de Visual Studio y un teléfono Android con depuración USB.

```bash
cd ProsegurGDT/ProsegurGDT.Movil
dotnet build -t:Install -c Debug
adb reverse tcp:5282 tcp:5282
```

En `Debug` la app habla con la web local (`http://localhost:5282`); en `Release`, con la web publicada en Azure.

## Seguridad

- Sesión obligatoria en toda la web y autorización por rol en cada controlador.
- Bloqueo de la cuenta tras 5 intentos fallidos y límite de intentos por dirección IP.
- Token antiforgery en todos los formularios; cookies `Secure`, `HttpOnly` y `SameSite`.
- Política de seguridad de contenido (CSP) con *nonce*, sin scripts en línea.
- API móvil con JWT de 10 horas, revocable al cerrar sesión o desactivar la cuenta.
- Auditoría de cada operación que cambia datos.
- Cifrado en reposo (TDE) y respaldos con restauración a un punto en el tiempo en Azure SQL.
- Acceso restringible a la red de la empresa (filtro por IP); la API móvil queda fuera del filtro.

No hay secretos en el repositorio: se configuran con *User Secrets* en local y con App Service Configuration en Azure.

## Estructura del repositorio

```
ProsegurGDT/
  ProsegurGDT.slnx
  ProsegurGDT.Web/      Web MVC y API REST
  ProsegurGDT.Movil/    App Android del repartidor
  ProsegurGDT.Tests/    Pruebas automatizadas
.github/workflows/      Integración continua
```
