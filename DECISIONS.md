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
- **Concurrencia: optimista, no un lock.** El saldo de créditos es un *token de concurrencia* de EF Core: el `UPDATE` solo se aplica si el saldo sigue siendo el que leímos. Si dos ingresos del mismo socio se pisan, el segundo `SaveChanges` falla con `DbUpdateConcurrencyException`, se descartan sus cambios y se **vuelve a decidir** con datos frescos (hasta 3 intentos). Al reintentar ya aparece el ingreso del primero, así que el segundo se resuelve como reingreso: se descuenta un solo crédito, que es lo que corresponde. Lo elegí sobre un `SemaphoreSlim` porque el lock solo protege dentro de un proceso, y sobre un `UPDATE ... WHERE CreditsRemaining > 0` porque ese evita saldos negativos pero **no** evita descontar dos créditos por una carrera de reingreso. Tiene tests que reproducen la carrera de verdad (`CheckInServiceTests`).
- **Migraciones de EF Core.** El esquema está versionado en `src/GymAccess/Migrations` y, al arrancar, la API aplica las pendientes con `Database.Migrate()` (si no existe la base, la crea). Sigue cumpliendo "`dotnet run` sin configuración adicional" y, a diferencia de `EnsureCreated`, permite evolucionar el esquema sin borrar datos. Contrapartida: cada cambio de modelo exige generar una migración.
- **Tests, en tres niveles**: unitarios de reglas (la mayoría, rápidos y sin infraestructura); del servicio contra SQLite en memoria, para lo que sí depende de la base (descuento persistido, reingreso, carreras de concurrencia); y de la API completa por HTTP con `WebApplicationFactory`, con reloj fijo, para rutas, validación, códigos de estado y serialización (por ejemplo, que `reason` viaje como texto). Postman queda como evidencia manual y ejecutable por quien evalúe.

## 3. Qué tocaría para agregar una regla de ingreso nueva

Ejemplo: "máximo 1 ingreso por día" o "socio suspendido".

1. **Crear la regla** en `src/GymAccess/Rules/`, implementando `ICheckInRule`. Es una clase de ~10 líneas.
2. **Registrarla en `CheckInRules.CreateDefault()`** en la posición que le corresponda: el orden es la prioridad del motivo.
3. **Si necesita un motivo nuevo**: agregar el valor a `DenialReason` y su texto en `CheckInMessages` (`Contracts/Responses.cs`). El motivo se serializa como string, así que no rompe a los clientes.
4. **Si necesita datos que hoy no se cargan** (por ejemplo, los ingresos del día, o un flag de suspensión en `Member`): agregarlos a `CheckInInput` / `CheckInContext` y cargarlos en `CheckInService` (donde se leen los datos del socio).
5. **Tests**: casos nuevos en `CheckInRulesTests` y quizás, una carpeta más en la colección de Postman

No hay que tocar `CheckInEvaluator`, los controllers ni el resto de las reglas.

## 4. Qué mejoraría con más tiempo

- **Idempotencia en `POST /check-ins`**: si el molinete reintenta por un timeout, hoy podría registrar dos ingresos.
- **Autenticación** (al menos una API key para el molinete) y autorización para los endpoints administrativos.
- **Paginación y filtros** en el historial de ingresos.
- **Modelo de franjas con más datos**: días de la semana, varias franjas por membresía.
- **Tolerancia por sede o por plan** en lugar de un único valor global.

## 5. Uso de IA

Usé **Claude** y **Cursor** como asistentes durante todo el proyecto: estructura de la solución, código, tests y colección de Postman. Las decisiones de diseño de la sección 1 (sobre todo cómo interactúan el reingreso y los créditos, y el orden de prioridad de los motivos) surgieron de charlar con la IA las dudas que me iban surgiendo.

**Cómo se refleja en el historial:** los primeros commits (esqueleto, dominio, reglas, tests, API, Postman y documentación) los generó la IA en bloque. Los cambios posteriores los fui aplicando y commiteando yo, uno por vez, corriendo `dotnet test` en cada paso: registro de intentos con DNI desconocido, concurrencia optimista, migraciones y tests de integración HTTP.

**Verificación:** el código inicial se generó sin poder compilarlo. Lo compilé y probé en mi máquina con .NET 8. Al sumar los tests HTTP aparecieron errores de compilación: el archivo de tests quedó mal armado y faltaban la referencia a `Microsoft.AspNetCore.Mvc.Testing` y la clase parcial `Program`. Los corregí y generé la migración inicial con `dotnet ef`, que no se había creado.

Resultado final:
- `dotnet test`: 59 tests, todos en verde.
- Colección de Postman corrida completa con el Collection Runner contra la API levantada: todos los requests pasan, incluido el ingreso aprobado y un rechazo por cada motivo (`NoActiveMembership`, `OverdueDebt`, `OutsideTimeSlot`, `NoCredits`, `MemberNotFound`).