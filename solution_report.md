El `StatisticsService` ha sido actualizado para ignorar los eventos `MatchStarted`, ya que la inicialización de las estadísticas ahora la maneja el `PersistenceService`. Esto debería resolver el error de "No se encontró la entidad de estadísticas para MatchId: 0".

Puedes intentar ejecutar tu aplicación de nuevo.