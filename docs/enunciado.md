# Twitch Analytics

# Introducción

Este es el reto técnico para personas que se han postulado a una posición en 540. El ejercicio está diseñado como una versión simplificada de algunos de los retos reales a los que nos enfrentamos en el día a día.

Más que evaluar tu habilidad para escribir el código "perfecto", queremos entender cómo piensas, cómo estructuras tu trabajo y cómo tomas decisiones técnicas. Este ejercicio es la base para una conversación posterior con parte del equipo técnico.

## Contexto

Twitch es la plataforma líder de streaming en directo para contenido relacionado con videojuegos y entretenimiento. A diario genera millones de horas de visualización y atrae a una enorme comunidad de usuarios y streamers.

En este reto, vamos a construir una pequeña API que interactúe con el sistema de Twitch para recuperar datos de usuarios y streams en vivo. Hemos simplificado el problema para centrarnos en lo que de verdad importa: **cómo construyes software de forma clara, segura y sostenible.**

## Objetivo del reto

Queremos que construyas dos endpoints que interactúen con la API pública de Twitch para:

1. **Consultar información de un usuario de Twitch por su ID**
2. **Consultar los streams que están en directo actualmente**

Además, deberás **gestionar correctamente la autenticación vía OAuth**, manejar errores esperables y devolver respuestas bien estructuradas, tal como se define más abajo.

## Sobre la realización de la prueba

- La prueba consta de dos partes:
    1. Una fase individual en la que desarrollarás la solución por tu cuenta.
    2. Una revisión conjunta en la que hablaremos sobre tu solución con parte del equipo técnico de 540.
- Todo el código deberá estar subido a un repositorio en GitHub, que te pedimos que compartas con nosotros una vez hayas terminado.
- Puedes hacer la prueba en el lenguaje y versión que prefieras, el que mejor domines y con el que te sientas más cómodo.
- Tú decides cómo organizar el código, estructurar el repositorio, qué herramientas utilizar y qué buenas prácticas aplicar.
- No buscamos que intentes impresionar ni que hagas algo innovador. Al contrario: queremos ver cómo trabajas en tu día a día, conocer tu estilo y poder conversar a partir de eso.
- Incluye un README con:
    - Cómo se ejecuta la aplicación.
    - Decisiones técnicas, trade-offs y cosas que dejaste fuera.
    - Cualquier duda o hipótesis que hayas hecho sobre el enunciado.
- Imagina que estás haciendo un pull request real para que lo revise una compañera del equipo.

<aside>
⚠️

**Tiempo estimado**

No deberías dedicarle más de **8 horas**. Si algo se queda fuera, explícalo.

</aside>

# Qué esperamos que implementes

## Endpoint 1: consultar información de un streamer de Twitch

### **Descripción**

Este endpoint permite consultar información de un streamer mediante su
ID. El sistema consulta la API de Twitch utilizando un token válido.

> *Para poder hacer peticiones a Twitch debemos tener un token de acceso de Twitch
(ver documentación). Si el token es inválido o ha expirado, el sistema deberá manejar el
error y solicitar la regeneración de un nuevo token si es necesario.*
> 

### **Flujo**

1. **Recepción de solicitud**: El sistema recibe una solicitud GET en el endpoint `/analytics/user` con un parámetro de consulta `id`, que es el identificador del usuario de Twitch.
2. **Validación del ID**: El sistema valida que el ID esté presente en la solicitud y que tenga un formato válido.
    - Si el ID está ausente o tiene un formato inválido, el sistema devuelve un error 400.
3. **Verificación del token de Twitch**: El sistema verifica si el token de acceso de Twitch es válido. Si el token ha expirado o es inválido:
    - Se devuelve un error 401 con un mensaje de error indicando que el token de acceso ha expirado.
    - Si el sistema puede regenerar el token automáticamente, lo hace antes de repetir la solicitud.

4. **Consulta a la API de Twitch**: El sistema realiza una consulta a la API de Twitch utilizando el ID proporcionado y el token válido.

- Si el ID del usuario no existe en Twitch, el sistema devuelve un error 404.

5. **Respuesta al cliente**:

- Si la consulta es exitosa, el sistema devuelve la información del usuario en el cuerpo de la respuesta.
- Si ocurre algún error inesperado, el sistema devuelve un error 500.

### **Llamadas y respuestas**

<aside>
⚠️

***Importante sobre las respuestas de los endpoints:*** Las **respuestas de los endpoints deben respetar exactamente** la estructura y los **campos definidos en los ejemplos de respuesta** proporcionados en el enunciado. Esto incluye tanto los nombres de los campos como su formato.

</aside>

**Llamada ejemplo**

```
GET /analytics/user?id=1234
Host: api.example.com
```

**Respuetas**

- 200 OK
    
    ```json
    {
      "id": "12345678",
      "login": "user_login",
      "display_name": "User Display Name",
      "type": "",
      "broadcaster_type": "partner",
      "description": "This is a generic description.",
      "profile_image_url": "<https://example.com/profile_image.png>",
      "offline_image_url": "<https://example.com/offline_image.jpeg>",
      "view_count": 0,
      "created_at": "2020-01-01T00:00:00Z"
    }
    ```
    
- 400 Bad Request
    
    ```json
    {
      "error": "Invalid or missing 'id' parameter."
    }
    ```
    
- 401 Unauthorized
    
    ```json
    {
      "error": "Unauthorized. Twitch access token is invalid or has expired."
    }
    ```
    
- 404 Not Found
    
    ```json
    {
      "error": "User not found."
    }
    ```
    
- 500 Internal Server Error
    
    ```json
    {
      "error": "Internal server error."
    }
    ```
    

## Endpoint 2: consultar streams en vivo

### **Descripción**

Este endpoint permite a los clientes obtener una lista de los streams que están actualmente en vivo en Twitch. El sistema consulta la API de Twitch utilizando un token de acceso. Si el token es inválido o ha expirado, el sistema manejará el error adecuadamente.

### **Flujo**

1. **Recepción de solicitud**: El sistema recibe una solicitud GET en el endpoint `/analytics/streams`.
2. **Verificación del token de Twitch**: El sistema verifica si el token de acceso de Twitch es válido. Si el token ha expirado o es inválido:
    - Se devuelve un error 401 con un mensaje de error indicando que el token de acceso ha expirado.
    - Si es posible, el sistema puede regenerar el token automáticamente antes de realizar la consulta.
3. **Consulta a la API de Twitch**: El sistema realiza una consulta a la API de Twitch utilizando el token válido.
4. **Respuesta al cliente**:
    - Si la consulta es exitosa, el sistema devuelve una lista de streams en vivo.
    - Si ocurre algún error inesperado, el sistema devuelve un error 500.

### **Llamadas y respuestas**

<aside>
⚠️

***Importante sobre las respuestas de los endpoints:*** Las **respuestas de los endpoints deben respetar exactamente** la estructura y los **campos definidos en los ejemplos de respuesta** proporcionados en el enunciado. Esto incluye tanto los nombres de los campos como su formato.

</aside>

**Llamada ejemplo**

```
GET /analytics/streams
Host: api.example.com
```

**Respuetas**

- 200 OK
    
    ```json
    [
      { "title": "Title of Stream 1", "user_name": "User1" },
      { "title": "Title of Stream 2", "user_name": "User2" },
      { "title": "Title of Stream 3", "user_name": "User3" }
    ]
    ```
    
- 401 Unauthorized
    
    ```json
    {
      "error": "Unauthorized. Twitch access token is invalid or has expired."
    }
    ```
    
- 500 Internal Server Error
    
    ```json
    {
      "error": "Internal server error."
    }
    ```
    

## Autenticación con Twitch

Para ahorrarte tiempo, puedes utilizar esta app ya registrada en Twitch para obtener el
token OAuth:

- **Client ID:** `******`
- **Client Secret:** `******`

Las credenciales se configuran con user secrets, ver README.md.

Puedes utilizar esta app para autenticarte y avanzar con la implementación sin tener que
crear una propia.

# Priorización de funcionalidades (según negocio)

En caso de que tengas que priorizar el desarrollo o simplificar alguna parte, te indicamos a
continuación el orden de prioridad de mayor a menor:

1. Happy paths funcionando correctamente (respuestas 200).
2. Gestión de tokens (respuestas 401).
3. Casos límite como usuarios no encontrados (respuestas 404).
4. Validaciones y errores generales (respuestas 400 y 500).

<aside>
⚠️

No hace falta cubrirlo todo al 100% si no tienes tiempo, pero sí nos interesa que priorices bien y nos expliques por qué has hecho lo que has hecho.

</aside>