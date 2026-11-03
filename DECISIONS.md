# Decisiones

## 1. Supuestos donde el enunciado no alcanzaba

**Fechas y horas**
- **Zona horaria.** "Hoy" y "la hora del ingreso" se calculan en la zona horaria del gimnasio, configurable (`Gym:TimeZoneId`, por defecto `America/Argentina/Buenos_Aires`). Todo se guarda en UTC. Sin esto, un ingreso a las 22:00 en Buenos Aires ya sería "mañana" para la membresía.
- **Vigente** = la fecha local está entre inicio y vencimiento, **ambos inclusive**.
- **Deuda vencida** = pendiente y con fecha de vencimiento **anterior** a hoy (el día del vencimiento todavía no cuenta como vencida).
- **Tolerancia**: se rechaza si el total vencido es **estrictamente mayor** que la tolerancia (igual a la tolerancia pasa). Por defecto 1000, configurable. El total suma todas las deudas pendientes vencidas del socio.
- **Franja horaria**: hora local, extremos inclusivos, y se admite que cruce la medianoche (22:00–02:00). No modelé días de la semana: el enunciado solo habla de franja horaria.

**Créditos y reingreso**
- Membresía sin `credits` = ilimitada. Guardo `Credits` (asignados) y `CreditsRemaining` (saldo). Admito `credits: 0` (pack agotado); es lo que permite mostrar el rechazo `NoCredits` en Postman sin esperar 2 horas.
- **Reingreso (regla 5)**:
  - La ventana de 2 h se mide desde el último ingreso que **descontó** crédito, no desde cualquier ingreso. Si los reingresos la renovaran, alguien podría encadenarlos para siempre sin pagar un crédito más.
  - Dentro de la ventana se **exime de la regla de créditos**, también con saldo 0: si usó su último crédito hace 20 minutos, puede volver a pasar.
  - Las demás reglas se siguen aplicando en el reingreso (membresía vigente, deuda, franja). Si el socio quedó con una deuda vencida o se le terminó la franja, no entra.
  - La ventana es por membresía (el ingreso queda asociado a la membresía que se usó).

**Varias membresías**
- Un socio puede tener más de una. Se evalúan en este orden: vigentes primero, **ilimitadas antes que con créditos** (para no gastar créditos al pedo) y luego la que vence antes. Entra por la primera que lo permita. Si ninguna, el motivo que se informa es el de la mejor candidata. No valido solapamiento.

**Respuestas**
- Si fallan varias reglas se informa **una sola**, la primera según el orden del enunciado (1→4).
- Un rechazo es **200** con `allowed: false` y `reason`. El endpoint funcionó; rechazar es el resultado de negocio. Reservo 4xx para errores del pedido (400 validación, 409 conflicto) y para recursos de los endpoints administrativos que no existen (404).
- **DNI inexistente** → 200 con `allowed: false` y `reason: MemberNotFound`. El enunciado dice que el ingreso registra "cada intento", así que el intento se guarda igual, con `MemberId` nulo y el DNI presentado (`PresentedDni`). Así el molinete recibe siempre el mismo formato de respuesta y queda auditoría de DNIs desconocidos.
- Cada intento se registra, aprobado o rechazado, con su motivo.
- El DNI se normaliza (se quitan puntos, espacios y guiones) y debe tener entre 6 y 9 dígitos.
- Pagar una deuda ya pagada → 409 (más útil para detectar errores del cliente que un 200 silencioso).

## 2. Por qué organicé el código así

- **Reglas puras separadas de la persistencia.** Cada regla es una clase chica (`ICheckInRule`) que recibe un `CheckInContext` y devuelve el motivo de rechazo o `null`. No leen la base ni mutan nada. `CheckInEvaluator` las encadena y devuelve una `CheckInDecision`. Resultado: las reglas se testean con objetos en memoria, rápido y sin EF, y el código que las prueba es casi un espejo del enunciado.
- **`CheckInService` es lo único con efectos.** Carga los datos, le pide la decisión al evaluador, descuenta el crédito y guarda el intento en un solo `SaveChanges` (atómico). Separar "decidir" de "aplicar" es lo que evita que una regla tenga efectos colaterales ocultos.
- **El orden de las reglas es explícito** (`CheckInRules.CreateDefault`) y define la prioridad del motivo de rechazo.
- **Reloj inyectable (`IClock`)** para testear ventanas de tiempo sin dormir ni depender de la hora real.
- **Controllers finos que usan el `DbContext` directamente** para los endpoints CRUD (crear socio, membresía, deuda). No metí repositorios ni capa de aplicación: serían pasamanos sin lógica. Si esos endpoints ganan reglas de negocio, el siguiente paso es sacarlas a servicios, como ya hice con el ingreso.
- **Concurrencia**: un `SemaphoreSlim` serializa los ingresos dentro del proceso, para que dos pasadas simultáneas del mismo socio no descuenten dos veces. Alcanza para una instancia con SQLite; ver mejoras.
- **`EnsureCreated()` en lugar de migraciones**, para cumplir con "`dotnet run` sin configuración adicional". Ver mejoras.
- **Tests**: unitarios de reglas (la mayoría), más tests del servicio contra SQLite en memoria para lo que sí depende de la base: descuento persistido, reingreso, registro de rechazos.

## 3. Qué tocaría para agregar una regla de ingreso nueva

Ejemplo: "máximo 1 ingreso por día" o "socio suspendido".

1. **Crear la regla** en `src/GymAccess/Rules/`, implementando `ICheckInRule`. Es una clase de ~10 líneas.
2. **Registrarla en `CheckInRules.CreateDefault()`** en la posición que le corresponda: el orden es la prioridad del motivo.
3. **Si necesita un motivo nuevo**: agregar el valor a `DenialReason` y su texto en `CheckInMessages` (`Contracts/Responses.cs`). El motivo se serializa como string, así que no rompe a los clientes.
4. **Si necesita datos que hoy no se cargan** (por ejemplo, los ingresos del día, o un flag de suspensión en `Member`): agregarlos a `CheckInInput` / `CheckInContext` y cargarlos en `CheckInService.RegisterAsync`.
5. **Tests**: casos nuevos en `CheckInRulesTests` y, si corresponde, una carpeta más en la colección de Postman.

No hay que tocar `CheckInEvaluator`, los controllers ni el resto de las reglas.

## 4. Qué mejoraría con más tiempo

- **Migraciones de EF Core** en lugar de `EnsureCreated` (hoy un cambio de esquema obliga a borrar `gymaccess.db`).
- **Concurrencia real**: descontar el crédito con un `UPDATE ... WHERE CreditsRemaining > 0` atómico (o un token de concurrencia) en lugar del semáforo en memoria, para que funcione con varias instancias o con otra base.
- **Idempotencia en `POST /check-ins`**: si el molinete reintenta por un timeout, hoy podría registrar dos ingresos.
- **Autenticación** (al menos una API key para el molinete) y autorización para los endpoints administrativos.
- **Tests de integración HTTP** con `WebApplicationFactory`, para cubrir serialización, validación y códigos de estado (hoy eso lo cubre la colección de Postman).
- **Paginación y filtros** en el historial de ingresos.
- **Modelo de franjas más rico**: días de la semana, varias franjas por membresía.
- **Operación**: Dockerfile, CI que corra `dotnet test`, logging estructurado de cada decisión de ingreso.
- **Tolerancia por sede o por plan** en lugar de un único valor global.

## 5. Uso de IA

Usé **Claude (Anthropic)** para construir este proyecto: la estructura del repo, el código, los tests, la colección de Postman y estos documentos. Las decisiones de diseño de la sección 1 (sobre todo cómo interactúan el reingreso y los créditos, y el orden de prioridad de los motivos) surgieron de ese trabajo y son las que yo debería poder defender en la conversación.

Limitación a tener en cuenta: el entorno donde se generó el código no tenía el SDK de .NET, por lo que no se pudo compilar ni correr `dotnet test` ahí. La verificación (build, tests y la colección de Postman contra la API levantada) hay que hacerla en una máquina con .NET 8.

<!-- TODO (completar antes de entregar): qué revisaste vos a mano, qué cambiaste y el resultado de dotnet test / Postman. -->
