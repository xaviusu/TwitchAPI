# TwitchAPI

API REST desarrollada con **ASP.NET Core y .NET 10**, organizada siguiendo los principios de **Clean Architecture**. Expone endpoints de analítica que consultan la API Helix de Twitch, manteniendo separadas las responsabilidades de la API, la lógica de aplicación, el dominio y las integraciones.

> **Estado del proyecto:** los dos endpoints (`GET /analytics/user` y `GET /analytics/streams`) y la integración con Twitch (token de aplicación OAuth con caché y renovación) están implementados.

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

Devuelve el usuario de Twitch con ese id numérico.

| Código | Cuándo |
| --- | --- |
| `200` | Usuario encontrado. |
| `400` | `id` ausente o no numérico. |
| `401` | Twitch rechaza el token incluso después de regenerarlo, o rechaza las credenciales de la aplicación. |
| `404` | Twitch no tiene ningún usuario con ese id. |
| `500` | Cualquier otro error: fallo o caída de Twitch, timeout (15 s), *rate limiting*, respuesta inválida o error inesperado. |

### `GET /analytics/streams`

Devuelve los streams que están en directo ahora mismo, con el título y el nombre del streamer:

```json
[
  { "title": "Title of Stream 1", "user_name": "User1" },
  { "title": "Title of Stream 2", "user_name": "User2" }
]
```

| Código | Cuándo |
| --- | --- |
| `200` | Lista de streams en directo (puede ser `[]`). |
| `401` | Twitch rechaza el token incluso después de regenerarlo, o rechaza las credenciales de la aplicación. |
| `500` | Cualquier otro error: fallo o caída de Twitch, timeout (15 s), *rate limiting*, respuesta inválida o error inesperado. |

### Formato de error

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
| **Solo 401 y 500 para los errores de Twitch** | El enunciado fija esos códigos y mensajes. Internamente cada fallo se clasifica (credenciales, timeout, *rate limiting*, caída) y queda en los logs; en un servicio real devolvería 502/503/504 para distinguir los fallos del servicio externo de los errores propios. |
| **Un único cliente de Twitch para los dos endpoints** | `TwitchApiClient` implementa `ITwitchUserClient` e `ITwitchStreamClient` y centraliza el token, el reintento ante un 401 y la traducción de errores, para no duplicar esa lógica. Si la integración creciera, se separaría en un cliente por recurso sobre un *handler* común. |
| **Renovación anticipada del token** | El token se renueva un 10 % antes de su caducidad (máximo 60 s) para no enviar tokens caducados; si Twitch responde 401, se invalida y se reintenta una vez. |
| **Análisis estático y formato compartidos** | Mejoran la consistencia entre los proyectos; requieren mantener las reglas acordadas y corregir las advertencias relevantes. |
| **SDK fijado mediante `global.json`** | Favorece la reproducibilidad del entorno y evita seleccionar accidentalmente un SDK preliminar más reciente. |

## Dudas e hipótesis

- **Formato válido del id:** los ids de usuario de Twitch son cadenas numéricas, así que se acepta cualquier id formado solo por dígitos ASCII. Un id con formato válido que no existe devuelve `404`.
- **Cuándo devolver `401`:** el enunciado pide devolver 401 si el token no es válido y, si es posible, regenerarlo. Se interpreta que primero se intenta regenerar: ante un 401 de Twitch se invalida el token, se pide uno nuevo y se repite la petición una vez. Solo se devuelve `401` si Twitch rechaza también el token nuevo, o si rechaza las credenciales de la aplicación al pedirlo.
- **Qué streams devolver:** Twitch tiene miles de streams en directo y su API los pagina. Se devuelve la primera página, con los **20** streams con más espectadores (el orden que usa Twitch), pidiéndolos explícitamente con `first=20`. Paginar, filtrar o cambiar el tamaño de página no se pide en el enunciado y quedaría como mejora (por ejemplo, con parámetros `first` y `after`).
- **Campos de la respuesta:** se devuelven exactamente los campos de los ejemplos del enunciado; el resto de datos que devuelve Twitch se descarta. `view_count` se mantiene aunque Twitch lo devuelve siempre a 0 desde que dejó de usarlo.
- **Errores no listados en el enunciado:** caídas, timeouts o *rate limiting* de Twitch se tratan como `500`, que es el error genérico que define el enunciado.
- **Credenciales:** aunque el enunciado proporciona un Client ID y un Client Secret, no se guardan en el repositorio; se configuran con *user secrets* o variables de entorno.

## Elementos excluidos deliberadamente

Por ahora **no se incluyen**:

- Entity Framework Core, migraciones, repositorios ni cualquier otra capa de persistencia.
- CQRS, MediatR o AutoMapper, porque no aportan una ventaja clara al alcance actual.
- Servicios e interfaces vacíos creados únicamente para completar la estructura.
- Configuración de autenticación propia, contenedores o despliegue sin un requisito que los justifique.
