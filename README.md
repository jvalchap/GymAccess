# GymAccess

API que decide si un socio puede ingresar a un gimnasio cuando presenta su DNI.
ASP.NET Core 8 (controllers) · EF Core + SQLite · xUnit.

> Decisiones, supuestos y cómo extenderlo: ver [DECISIONS.md](DECISIONS.md).

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

No hace falta nada más: la base SQLite (`gymaccess.db`) se crea sola al arrancar.

## Correr la API

```bash
dotnet run --project src/GymAccess
```

Queda escuchando en `http://localhost:5080` (Swagger en `http://localhost:5080/swagger`).

## Correr los tests

```bash
dotnet test
```

Son tests de las reglas de ingreso (puros, sin base de datos) y de la orquestación contra SQLite en memoria.

## Configuración

`src/GymAccess/appsettings.json`:

```json
"Gym": {
  "DebtTolerance": 1000,
  "ReentryWindowHours": 2,
  "TimeZoneId": "America/Argentina/Buenos_Aires"
}
```

| Clave | Qué hace |
|---|---|
| `DebtTolerance` | Monto de tolerancia: se rechaza si las deudas pendientes **vencidas** suman **más** que esto. |
| `ReentryWindowHours` | Ventana de reingreso sin descontar otro crédito (2 por enunciado). |
| `TimeZoneId` | Zona horaria del gimnasio: define "hoy" y la hora local para vigencia, deudas y franja horaria. |

Cualquier valor se puede pisar por variable de entorno, por ejemplo `Gym__DebtTolerance=0 dotnet run --project src/GymAccess`.

## Endpoints

| Método | Ruta | Descripción | Respuestas |
|---|---|---|---|
| POST | `/members` | Crea un socio | 201 · 400 · 409 (DNI duplicado) |
| GET | `/members/{id}` | Datos de un socio (extra, para el `Location` del 201) | 200 · 404 |
| POST | `/members/{id}/memberships` | Asigna una membresía | 201 · 400 · 404 |
| POST | `/members/{id}/debts` | Registra una deuda | 201 · 400 · 404 |
| POST | `/debts/{id}/pay` | Marca una deuda como pagada | 200 · 404 · 409 (ya pagada) |
| POST | `/check-ins` | Recibe un DNI y decide el ingreso | 200 (aprobado **o** rechazado) · 404 (DNI desconocido) |
| GET | `/members/{id}/check-ins` | Historial de ingresos, del más reciente al más antiguo | 200 · 404 |

### Respuesta de `POST /check-ins`

Un rechazo es una decisión válida, por lo que responde **200** con `allowed: false` y el motivo en `reason`:

| `reason` | Regla |
|---|---|
| `NoActiveMembership` | 1. No tiene una membresía vigente |
| `OverdueDebt` | 2. Las deudas pendientes vencidas superan la tolerancia |
| `OutsideTimeSlot` | 3. Fuera de la franja horaria de la membresía |
| `NoCredits` | 4. La membresía usa créditos y no quedan |

Si fallan varias reglas, se informa la primera en ese orden.

## Ejemplos de uso

```bash
# 1. Crear un socio
curl -s -X POST http://localhost:5080/members \
  -H "Content-Type: application/json" \
  -d '{"name": "Ana Pérez", "dni": "30.111.222"}'
# → 201 {"id":1,"name":"Ana Pérez","dni":"30111222"}

# 2. Asignarle una membresía de 10 créditos, de lunes a viernes de 7:00 a 22:00 (franja opcional)
curl -s -X POST http://localhost:5080/members/1/memberships \
  -H "Content-Type: application/json" \
  -d '{"startDate": "2026-11-01", "endDate": "2026-12-31", "credits": 10, "slotStart": "07:00", "slotEnd": "22:00"}'
# → 201 {"id":1,"memberId":1,"startDate":"2026-11-01","endDate":"2026-12-31","credits":10,"creditsRemaining":10,"slotStart":"07:00:00","slotEnd":"22:00:00"}
#   (sin "credits" → ilimitada; sin "slotStart"/"slotEnd" → a cualquier hora)

# 3. Registrar una deuda
curl -s -X POST http://localhost:5080/members/1/debts \
  -H "Content-Type: application/json" \
  -d '{"amount": 5000, "dueDate": "2026-10-15"}'
# → 201 {"id":1,"memberId":1,"amount":5000,"dueDate":"2026-10-15","status":"Pending","paidAt":null}

# 4. Intentar ingresar (rechazado por la deuda vencida)
curl -s -X POST http://localhost:5080/check-ins \
  -H "Content-Type: application/json" \
  -d '{"dni": "30111222"}'
# → 200 {"id":1,"memberId":1,"allowed":false,"reason":"OverdueDebt","message":"El socio tiene deudas vencidas por encima del monto de tolerancia.","membershipId":null,"creditConsumed":false,"creditsRemaining":null,"occurredAt":"..."}

# 5. Pagar la deuda
curl -s -X POST http://localhost:5080/debts/1/pay
# → 200 {"id":1,...,"status":"Paid","paidAt":"..."}

# 6. Ingresar de nuevo (aprobado, descuenta un crédito)
curl -s -X POST http://localhost:5080/check-ins \
  -H "Content-Type: application/json" \
  -d '{"dni": "30111222"}'
# → 200 {"id":2,"memberId":1,"allowed":true,"reason":null,"message":"Ingreso permitido. Se descontó un crédito.","membershipId":1,"creditConsumed":true,"creditsRemaining":9,"occurredAt":"..."}

# 7. Volver a pasar dentro de las 2 horas (aprobado, NO descuenta)
#    → "creditConsumed": false, "creditsRemaining": 9

# 8. Historial
curl -s http://localhost:5080/members/1/check-ins
```

Los errores de validación y los 404/409 usan el formato estándar `ProblemDetails`.

## Evidencia: colección de Postman

[`postman/GymAccess.postman_collection.json`](postman/GymAccess.postman_collection.json)

1. Levantar la API (`dotnet run --project src/GymAccess`).
2. Importar la colección en Postman y correrla completa con el **Collection Runner** (o con Newman: `newman run postman/GymAccess.postman_collection.json`).

No necesita datos cargados de antemano: cada carpeta crea su propio socio con un DNI aleatorio y las fechas y franjas horarias se calculan relativas al momento de la ejecución (la franja "adentro/afuera" se arma a partir de la hora actual de Buenos Aires). Cada request trae tests con la respuesta esperada y una descripción de lo que debería devolver.

| Carpeta | Caso |
|---|---|
| 1 | **Aprobado**: membresía vigente e ilimitada |
| 2 | **Aprobado**: dentro de la franja horaria |
| 3 | **Rechazado** `NoActiveMembership` |
| 4 | **Rechazado** `OverdueDebt` (también: deuda dentro de la tolerancia, pago, y 409 al pagar dos veces) |
| 5 | **Rechazado** `OutsideTimeSlot` |
| 6 | **Rechazado** `NoCredits` |
| 7 | Créditos: el primer ingreso descuenta; el reingreso dentro de 2 h no. Historial. |
| 8 | Reingreso con el último crédito |
| 9 | Errores: DNI inexistente, DNI duplicado, validaciones, 404 |

> Nota sobre la carpeta 6: agotar créditos "de verdad" exige esperar 2 horas (por la regla de reingreso), así que para poder mostrarlo en el momento se asigna una membresía con `credits: 0`. El flujo real (consumir el último crédito → reingresar → pasadas las 2 h, rechazo) está en `CheckInServiceTests.Last_credit_can_be_reentered_and_then_is_exhausted_after_the_window`.

## Estructura

```
src/GymAccess/
  Domain/         Entidades (Member, Membership, Debt, CheckIn) y enums
  Rules/          Reglas de ingreso (puras) y el evaluador que las encadena
  Services/       CheckInService (orquesta y persiste), reloj, helpers
  Data/           DbContext de EF Core
  Controllers/    Endpoints HTTP
  Contracts/      DTOs de request/response
tests/GymAccess.Tests/
postman/
```
