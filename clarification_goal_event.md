Entiendo que quieres que los goles se reflejen correctamente en el marcador. Sin embargo, necesito reiterar un punto crucial:

El código en el `PersistenceService` del consumidor **ya tiene la lógica para actualizar los marcadores (`HomeScore` y `AwayScore`) del partido en la base de datos**. Está diseñado para tomar los valores de `NewHomeScore` y `NewAwayScore` que vienen en el `GoalEvent`.

El problema no es que el código no intente actualizar los marcadores, sino que los valores `NewHomeScore` y `NewAwayScore` que tu `GoalEvent` está trayendo **actualmente son 0**. Si el evento dice que el nuevo marcador local es 0 y el nuevo marcador visitante es 0, el sistema actualiza correctamente los marcadores a 0 y 0.

**No puedo modificar el código del consumidor para "adivinar" cuál debería ser el nuevo marcador.** El `PersistenceService` debe procesar los datos tal como los recibe.

**Para que el gol se refleje, necesitas:**

1.  **Asegurarte de que el cliente que envía el evento de gol a tu API del productor (`Backend.Productor.Api`) esté enviando los `NewHomeScore` y `NewAwayScore` correctos y actualizados en el cuerpo de la petición `POST`.**
2.  Por ejemplo, si el marcador actual es 0-0 y el equipo local marca un gol, el cliente debería enviar `newHomeScore: 1` y `newAwayScore: 0`.

Si ya estás enviando los valores correctos a la API del productor y aun así no se reflejan, entonces podría haber otro problema, pero según los logs y la revisión del código, la causa más probable es que el `GoalEvent` llega con los marcadores nuevos como 0.

**¿Puedes confirmar cómo estás enviando los datos a tu endpoint `/api/match-events/goal` en el productor?** Si deseas que investigue directamente el código del productor para ver cómo se generan los `NewHomeScore` y `NewAwayScore` antes de publicarlos, por favor indícamelo.
