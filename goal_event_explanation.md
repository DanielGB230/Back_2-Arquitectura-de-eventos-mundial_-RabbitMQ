He investigado el motivo por el que los goles no se actualizan y he confirmado que el problema no está en el consumidor (`Backend.Consumidor.Api`) que hemos estado arreglando. El error se origina en los datos que se envían al productor (`Backend.Productor.Api`).

Aquí está el flujo completo y dónde se encuentra el problema:

1.  **Un cliente (ej. Postman, otra app) envía un evento de Gol**: Se hace una petición `POST` a la API del productor (`Backend.Productor.Api`) al endpoint `/api/match-events/goal`.
2.  **El problema está aquí**: Es muy probable que el cuerpo de esta petición `POST` que se está enviando contenga los valores `newHomeScore: 0` y `newAwayScore: 0`.
3.  **API Productor**: Tu API del productor (`MatchEventService`) recibe estos valores (0 y 0) y los publica correctamente en un mensaje `GoalEvent` en RabbitMQ.
4.  **API Consumidor**: Tu `PersistenceService` en el consumidor recibe este mensaje `GoalEvent` con los marcadores en 0.
5.  **Actualización en BD**: El `PersistenceService` intenta actualizar el `Match` en la base de datos con los valores que recibió (0 y 0). Como el marcador en la base de datos ya es 0 y 0, Entity Framework no detecta ningún cambio y, por lo tanto, no ejecuta una sentencia `UPDATE`.

**Conclusión:**

El código en `Backend.Consumidor.Api` es **correcto**. El `PersistenceService` está listo para actualizar el marcador, pero está recibiendo los datos incorrectos.

**Acción Requerida:**

Debes revisar el cliente o la herramienta que estás usando para simular el evento de gol. Asegúrate de que cuando haces la llamada `POST` a `/api/match-events/goal`, el cuerpo JSON que envías contenga los **nuevos marcadores actualizados**.

Por ejemplo, si el marcador es 0-0 y el equipo local anota, el cuerpo de tu petición debería ser algo así:

```json
{
  "matchId": 4,
  "teamId": 123,    // ID del equipo que anotó
  "playerId": 456,  // ID del jugador que anotó
  "minute": 55,
  "newHomeScore": 1, // <--- VALOR CORRECTO
  "newAwayScore": 0
}
```

Revisa cómo estás enviando estos eventos y el problema se resolverá.
