# TwitchAPI

API REST desarrollada con **ASP.NET Core y .NET 10**, organizada siguiendo los principios de **Clean Architecture**. Expone endpoints de analítica que consultan la API Helix de Twitch, manteniendo separadas las responsabilidades de la API, la lógica de aplicación, el dominio y las integraciones.

> **Estado del proyecto:** el endpoint `GET /analytics/user` y la integración con Twitch (token de aplicación OAuth con caché y renovación) están implementados. El segundo endpoint está pendiente.

## Requisitos

- **.NET 10 SDK**. El archivo `global.json` define la versión de referencia y la política de actualización admitida.
- Un entorno compatible con .NET (por ejemplo, Visual Studio o la CLI de .NET).
- Para ejecutar el perfil HTTPS en local, un certificado de desarrollo de ASP.NET Core confiable.
- Una aplicación registrada en la [consola de desarrolladores de Twitch](https://dev.twitch.tv/console/apps) (Client ID y Client Secret).

No se necesita instalar ni configurar una base de datos.

## Ejecución en local

Los siguientes comandos se ejecutan desde la **raíz del repositorio**.

1. Restaurar dependencias:

   ```powershell
   dotnet restore
   ```

2. Compilar la solución:

   ```powershell
   dotnet build
   ```

3. Configurar las credenciales de Twitch con *user secrets* (nunca en `appsettings.json`):

   ```powershell
   dotnet user-secrets set "Twitch:ClientId" "<tu-client-id>" --project .\src\TwitchAPI.Api
   dotnet user-secrets set "Twitch:ClientSecret" "<tu-client-secret>" --project .\src\TwitchAPI.Api
   ```

   La aplicación no arranca si faltan. En otros entornos se pueden definir con las variables `Twitch__ClientId` y `Twitch__ClientSecret`.

4. Confiar en el certificado HTTPS de desarrollo (solo si aún no está configurado):

   ```powershell
   dotnet dev-certs https --trust
   ```

5. Iniciar la API mediante el perfil HTTPS:

   ```powershell
   dotnet run --project .\src\TwitchAPI.Api --launch-profile https
   ```

La aplicación mostrará en la consola las direcciones en las que está escuchando. Durante el desarrollo, la especificación OpenAPI está disponible en:

```text
https://localhost:<puerto-https>/openapi/v1.json
```

Sustituye `<puerto-https>` por el indicado en la consola.

## Endpoints

### `GET /analytics/user?id={id}`

Devuelve el usuario de Twitch con ese id numérico (máximo 20 dígitos).

| Código | Cuándo |
| --- | --- |
| `200` | Usuario encontrado. |
| `400` | `id` ausente, no numérico o demasiado largo. |
| `404` | Twitch no tiene ningún usuario con ese id. |
| `502` | Twitch falló, devolvió una respuesta inválida o rechazó las credenciales de la aplicación. |
| `503` | Twitch aplicó *rate limiting*; incluye la cabecera `Retry-After` cuando se conoce. |
| `504` | Twitch no respondió a tiempo (15 s). |
| `500` | Error inesperado. |

Todos los errores, incluidas las rutas inexistentes, usan el mismo formato:

```json
{ "error": "User not found." }
```

## Pruebas y calidad de código

Ejecutar las pruebas automatizadas:

```powershell
dotnet test
```

Verificar el formato y las reglas de estilo sin modificar archivos:

```powershell
dotnet format --verify-no-changes
```

La solución incluye un proyecto de pruebas con **xUnit**: pruebas unitarias del validador, del contrato JSON, del proveedor de tokens y del cliente de Twitch (con `HttpMessageHandler` simulados), y pruebas de integración de los endpoints con `WebApplicationFactory`. Las pruebas no llaman a Twitch ni necesitan credenciales reales: `TestWebApplicationFactory` inyecta credenciales ficticias.

Las reglas compartidas de compilación y análisis están definidas en `Directory.Build.props`; las convenciones de formato y estilo están en `.editorconfig`.

## Estructura y arquitectura

```text
TwitchAPI/
├── src/
│   ├── TwitchAPI.Api/
│   ├── TwitchAPI.Application/
│   ├── TwitchAPI.Domain/
│   └── TwitchAPI.Infrastructure/
├── tests/
│   └── TwitchAPI.Tests/
├── .editorconfig
├── .gitignore
├── Directory.Build.props
├── global.json
└── README.md
```

| Proyecto | Responsabilidad |
| --- | --- |
| **TwitchAPI.Api** | Exposición de endpoints mediante controladores, configuración HTTP y punto de entrada de la aplicación. |
| **TwitchAPI.Application** | Casos de uso, servicios y contratos requeridos por la aplicación. |
| **TwitchAPI.Domain** | Modelos y reglas de negocio, cuando sean necesarios. |
| **TwitchAPI.Infrastructure** | Implementaciones técnicas de integraciones externas. |
| **TwitchAPI.Tests** | Pruebas unitarias y de integración. |

Las dependencias entre proyectos siguen esta dirección:

- `Api` → `Application`.
- `Application` → `Domain`.
- `Infrastructure` → `Application`.
- `Api` → `Infrastructure` **solo en el punto de composición** para registrar implementaciones mediante inyección de dependencias.

Los controladores no deben utilizar directamente clases concretas de Infrastructure. La capa Application define los contratos que Infrastructure implementa.

## Decisiones técnicas y compromisos

| Decisión | Motivo y contrapartida |
| --- | --- |
| **.NET 10 y ASP.NET Core** | Plataforma principal de la API, con soporte para controladores, inyección de dependencias y OpenAPI. |
| **Cuatro proyectos de producción separados** | Facilitan identificar responsabilidades y probar componentes de forma aislada. A cambio, introducen más proyectos y referencias que una API monolítica pequeña. |
| **Controladores en lugar de Minimal APIs** | Hacen explícita la organización de los endpoints; para solo dos rutas, Minimal APIs también sería una alternativa válida. |
| **Sin base de datos** | Los requisitos actuales solo contemplan comunicación con un servicio externo. Se evita añadir almacenamiento y persistencia innecesarios. |
| **xUnit y pruebas de integración** | Permiten verificar el comportamiento HTTP sin ejecutar manualmente un servidor ni depender de Twitch. |
| **Errores de Twitch como 502/503/504** | Un fallo del servicio externo (incluidas credenciales de aplicación rechazadas) no es culpa del cliente de esta API, así que no se devuelve 401. El detalle queda en los logs. |
| **Renovación anticipada del token** | El token se renueva un 10 % antes de su caducidad (máximo 60 s) para no enviar tokens caducados; si Twitch responde 401, se invalida y se reintenta una vez. |
| **Análisis estático y formato compartidos** | Mejoran la consistencia entre los proyectos; requieren mantener las reglas acordadas y corregir las advertencias relevantes. |
| **SDK fijado mediante `global.json`** | Favorece la reproducibilidad del entorno y evita seleccionar accidentalmente un SDK preliminar más reciente. |

## Elementos excluidos deliberadamente

Por ahora **no se incluyen**:

- Entity Framework Core, migraciones, repositorios ni cualquier otra capa de persistencia.
- CQRS, MediatR o AutoMapper, porque no aportan una ventaja clara al alcance actual.
- Servicios e interfaces vacíos creados únicamente para completar la estructura.
- Configuración de autenticación propia, contenedores o despliegue sin un requisito que los justifique.

## Próximos pasos

- Implementar el **segundo endpoint**.
- Preparar la automatización de compilación y pruebas en GitHub Actions.

Estas tareas describen el trabajo previsto, **no funcionalidades ya disponibles**.
