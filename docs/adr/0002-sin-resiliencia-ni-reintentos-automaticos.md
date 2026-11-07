# Sin resiliencia ni reintentos automáticos hacia Twitch

Las llamadas a Twitch no usan `Microsoft.Extensions.Http.Resilience` (Polly) ni ningún reintento automático. Solo hay un timeout de `HttpClient` de unos 10 s, y si salta se devuelve 500.

El único reintento es explícito: una vez, tras renovar el App Access Token cuando Twitch responde 401. El handler estándar de resiliencia reintenta por su cuenta los 5xx, los 429 y los timeouts. Si lo combináramos con nuestro reintento por 401, sería difícil saber cuántas llamadas hace cada petición y cuánto puede tardar en el peor caso. Además, ninguno de esos reintentos cambiaría el contrato: el cliente seguiría recibiendo 200 o 500.

## Consequences

Un fallo transitorio de Twitch llega al cliente como 500 aunque un reintento lo hubiera resuelto. Si hiciera falta, se puede añadir más adelante un handler de resiliencia con una política acotada (sin reintentar los 401, para no duplicar el nuestro). Queda anotado en "cosas que dejé fuera" del README.
