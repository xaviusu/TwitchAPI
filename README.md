# TwitchAPI

API REST desarrollada con **ASP.NET Core y .NET 10**, organizada siguiendo los principios de **Clean Architecture**. Su objetivo es exponer dos endpoints que se comunicarán con un servicio externo, manteniendo separadas las responsabilidades de la API, la lógica de aplicación, el dominio y las integraciones.

> **Estado del proyecto:** `GET /analytics/streams` funciona en el happy path (200), con un App Access Token obtenido por client credentials. Pendientes: `GET /analytics/user`, la caché y renovación del token (401) y el formato de errores 400/500 del enunciado.

## Requisitos

- **.NET 10 SDK**. El archivo `global.json` define la versión de referencia y la política de actualización admitida.
- Un entorno compatible con .NET (por ejemplo, Visual Studio o la CLI de .NET).
- Para ejecutar el perfil HTTPS en local, un certificado de desarrollo de ASP.NET Core confiable.

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

3. Configurar las credenciales de Twitch con **user secrets** (nunca en `appsettings*.json`):

   ```powershell
   dotnet user-secrets set "Twitch:ClientId" "<client-id>" --project .\src\TwitchAPI.Api
   dotnet user-secrets set "Twitch:ClientSecret" "<client-secret>" --project .\src\TwitchAPI.Api
   ```

   Fuera de Development, usa variables de entorno (`Twitch__ClientId` y `Twitch__ClientSecret`). Si falta alguna de las dos, la aplicación no arranca.

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

Sustituye `<puerto-https>` por el indicado en la consola. Para consultar los Live Streams:

```text
GET https://localhost:<puerto-https>/analytics/streams
```

### Configuración opcional

| Clave | Valor por defecto | Uso |
| --- | --- | --- |
| `Twitch:AuthBaseAddress` | `https://id.twitch.tv/` | Endpoint de tokens (`oauth2/token`). |
| `Twitch:HelixBaseAddress` | `https://api.twitch.tv/helix/` | API Helix. |
| `Twitch:Timeout` | `00:00:10` | Timeout de cada llamada a Twitch. |

## Pruebas y calidad de código

Ejecutar las pruebas automatizadas:

```powershell
dotnet test
```

Verificar el formato y las reglas de estilo sin modificar archivos:

```powershell
dotnet format --verify-no-changes
```

Las pruebas son de integración por HTTP con **xUnit** y `WebApplicationFactory<Program>`. El factory de test (`TwitchApiFactory`) inyecta credenciales falsas, sustituye `TimeProvider` por `FakeTimeProvider` y cambia el `HttpMessageHandler` primario de los `HttpClient` por un **Twitch falso** (`FakeTwitch`) que responde a `/oauth2/token` y a `/helix/*` y registra las peticiones. Las pruebas no llaman a Twitch real ni dependen de user secrets.

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
| **xUnit y pruebas de integración** | Permiten verificar el comportamiento HTTP sin ejecutar manualmente un servidor. Las pruebas de los futuros servicios y clientes externos se añadirán al implementarlos. |
| **Análisis estático y formato compartidos** | Mejoran la consistencia entre los proyectos; requieren mantener las reglas acordadas y corregir las advertencias relevantes. |
| **SDK fijado mediante `global.json`** | Favorece la reproducibilidad del entorno y evita seleccionar accidentalmente un SDK preliminar más reciente. |

## Elementos excluidos deliberadamente

Por ahora **no se incluyen**:

- Entity Framework Core, migraciones, repositorios ni cualquier otra capa de persistencia.
- CQRS, MediatR o AutoMapper, porque no aportan una ventaja clara al alcance actual.
- Servicios e interfaces vacíos creados únicamente para completar la estructura.
- Configuración de autenticación propia, contenedores o despliegue sin un requisito que los justifique.

## Dudas e hipótesis

- **Live Streams sin paginación.** `/analytics/streams` hace una sola llamada a Helix `GET /helix/streams?first=100` (el máximo por página) y no sigue el cursor. El enunciado no pide paginación ni parámetros, y recorrer todas las páginas supondría miles de llamadas. Se devuelven en el orden de Twitch (de más a menos espectadores).

## Próximos pasos

- `GET /analytics/user` (200 y 404).
- Caché, renovación e invalidación del App Access Token, con un único reintento cuando Twitch responde 401.
- Validación del `id` (400) y cuerpo `{ "error": ... }` del enunciado para los fallos inesperados (500).
- Preparar la automatización de compilación y pruebas en GitHub Actions.

Estas tareas describen el trabajo previsto, **no funcionalidades ya disponibles**.
