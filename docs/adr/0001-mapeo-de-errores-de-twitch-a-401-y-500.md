# Los errores de Twitch se traducen solo a 401 o 500

El enunciado solo define 401 y 500 como errores originados en Twitch (los 400 y 404 son nuestros: validación del `id` y `data` vacío en Get Users). Por eso, cualquier respuesta de Twitch que no sea un éxito se traduce así:

- **401**: Twitch sigue respondiendo 401 después de renovar el App Access Token y reintentar una vez, o `id.twitch.tv` rechaza las credenciales.
- **500**: todo lo demás, con `{"error": "Internal server error."}`. Incluye un 400 de Twitch, 429, 5xx, timeouts, errores de red, JSON inesperado y la caída del endpoint de tokens.

No exponemos 429, 502 ni 503 aunque serían más descriptivos, porque la regla es que el enunciado manda. El detalle real se queda en los logs.

## Considered Options

- **Traducir un 400 de Twitch a 404 en Get Users.** Lo descartamos: la referencia de Get Users solo documenta el 400 para un `id`/`login` ausente o para demasiados parámetros, nunca para un usuario que no existe. Con un `id` bien formado, que Twitch responda 400 sería un fallo inesperado, no un "User not found".
