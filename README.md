# TwitchAPI

API REST desarrollada con **ASP.NET Core y .NET 10**, organizada siguiendo los principios de **Clean Architecture**. Su objetivo es exponer dos endpoints que se comunicarán con un servicio externo, manteniendo separadas las responsabilidades de la API, la lógica de aplicación, el dominio y las integraciones.

> **Estado del proyecto:** `GET /analytics/streams` (200 y 401) y `GET /analytics/user` (200, 400, 401 y 404) funcionan con un App Access Token obtenido por client credentials, cacheado en memoria y renovado cuando caduca o cuando Twitch lo rechaza. Pendiente: el formato de errores 500 del enunciado.

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

Sustituye `<puerto-https>` por el indicado en la consola. Para consultar los Live Streams y un Twitch User por su Twitch User ID:

```text
GET https://localhost:<puerto-https>/analytics/streams
GET https://localhost:<puerto-https>/analytics/user?id=141981764
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

La única excepción a esa seam única es el test de concurrencia del App Access Token: el factory envuelve el proveedor real del token (`AccessTokenWaiters`) para contar las peticiones que ya están esperando la renovación. Desde el Twitch falso solo se ve la petición que llega a `/oauth2/token`; las demás esperan dentro de la API. Sin ese contador, saber cuándo soltar la respuesta del token dependería de una espera real y el test no sería determinista. El envoltorio no cambia el comportamiento: delega siempre en el proveedor real.

Las reglas compartidas de compilación y análisis están definidas en `Directory.Build.props`; las convenciones de formato y estilo están en `.editorconfig`.

GitHub Actions (`.github/workflows/ci.yml`) ejecuta en cada pull request y en cada push a cualquier rama la compilación con warnings como errores (`-warnaserror`), las pruebas y `dotnet format --verify-no-changes`.

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
| **App Access Token cacheado en memoria** | Un singleton guarda el token y lo reutiliza hasta un minuto antes de `expires_in`, con el tiempo de `TimeProvider` (sustituible en tests). La renovación va protegida por un `SemaphoreSlim`, así que con muchas peticiones simultáneas solo una pide token nuevo. A cambio, cada instancia de la API tiene su propio token y se pierde al reiniciar; para una sola instancia no compensa una caché distribuida. |
| **Errores con el cuerpo del enunciado, sin `ProblemDetails`** | El enunciado fija los cuerpos de error como `{ "error": "<mensaje>" }`, así que no se usan `AddProblemDetails` ni `UseStatusCodePages`. Los cuatro mensajes literales viven en `ErrorResponse`. La validación automática de `[ApiController]` devuelve el 400 del enunciado para cualquier entrada inválida; como `/analytics/user` es el único endpoint con parámetros, ese 400 siempre se refiere al `id` (un endpoint futuro con otros parámetros necesitaría su propio mensaje). Además, el formato del `id` lo comprueba el tipo de dominio `TwitchUserId` mediante un model binder propio, de modo que el controlador y el cliente de Twitch nunca reciben un `id` mal formado. Las rutas inexistentes (404) y los métodos no admitidos (405) mantienen la respuesta vacía por defecto del framework, porque el enunciado no las define. |
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
- **Twitch User inexistente → 404.** `/analytics/user` hace una sola llamada a Helix `GET /helix/users?id=<id>`. Si Twitch responde `200` con `data: []`, devolvemos 404 `{"error": "User not found."}`. Que un `id` inexistente produzca `data` vacío es comportamiento observado de Twitch, no documentado en la referencia de Get Users.
- **Campos del Twitch User tal cual.** Se devuelven exactamente los diez campos del ejemplo del enunciado, en ese orden, con los valores que da Twitch sin transformar: `created_at` se copia como string, `view_count` es numérico (Twitch lo tiene deprecado y puede valer 0) y los strings vacíos llegan como `""`. Los campos que Twitch añade y el enunciado no define (por ejemplo `email`) no se exponen.
- **Formato aceptado del `id`.** El enunciado pide validar que el `id` esté presente y tenga "un formato válido", sin definirlo. Los Twitch User ID son strings numéricos, así que aceptamos un `id` si y solo si aparece **una única vez** en la query y cumple `^[0-9]+$` con una longitud de 1 a 20 caracteres (20 dígitos caben de sobra en cualquier ID actual de Twitch y acotan la entrada). Se rechazan con 400 `{"error": "Invalid or missing 'id' parameter."}`, sin llamar a Twitch: el `id` ausente o vacío, letras, signos (`-5`, `+5`), espacios alrededor del número, dígitos no ASCII, más de 20 dígitos y el `id` repetido (`id=1&id=2`, incluso con el mismo valor). Un `id` bien formado que no existe en Twitch sigue dando 404.
- **Cuándo se devuelve 401.** El enunciado pide devolver 401 si el token es inválido o ha caducado, pero también regenerarlo si es posible. Interpretamos que el cliente solo debe ver el 401 cuando la API no puede recuperarse sola: si Helix responde 401, se invalida el App Access Token, se pide uno nuevo y se reintenta **una sola vez**; solo si ese reintento vuelve a dar 401, o si `id.twitch.tv` rechaza las credenciales (400, 401 o 403), se devuelve 401 `{"error": "Unauthorized. Twitch access token is invalid or has expired."}`. Cualquier otro fallo del endpoint de tokens no es un problema de autenticación (ver [ADR-0001](docs/adr/0001-mapeo-de-errores-de-twitch-a-401-y-500.md)).

## Próximos pasos

- Cuerpo `{ "error": ... }` del enunciado para los fallos inesperados (500). Hasta entonces, una excepción no controlada devuelve 500 sin cuerpo.

Estas tareas describen el trabajo previsto, **no funcionalidades ya disponibles**.
