using System.Text.Json;
using Shared.Contracts.Events;
using Resend;
using Microsoft.Extensions.Logging;

namespace Backend.Consumidor.Api.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IResend _resend;

    public EmailService(ILogger<EmailService> logger, IResend resend)
    {
        _logger = logger;
        _resend = resend;
    }

    // 1. CAMBIO DE NOMBRE: Renombramos el parámetro a 'jsonEventString' para no chocar con el otro 'message'
    public async Task HandleEventAsync(string jsonEventString)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Usamos el nombre nuevo aquí
        using JsonDocument jsonDoc = JsonDocument.Parse(jsonEventString);
        var root = jsonDoc.RootElement;

        if (!root.TryGetProperty("EventType", out var eventTypeElement) || !eventTypeElement.TryGetByte(out var eventTypeByte))
        {
            _logger.LogWarning("EMAIL-SERVICE: No se pudo determinar el EventType del mensaje.");
            return;
        }

        var eventType = (Shared.Contracts.Enums.EventType)eventTypeByte;

        // Manejo seguro por si MatchId no viene
        int matchId = 0;
        if (root.TryGetProperty("MatchId", out var matchIdElem))
        {
            matchId = matchIdElem.GetInt32();
        }

        var subject = $"Alerta de Evento - {eventType} en Partido {matchId}";

        // Usamos el nombre nuevo aquí también
        var formattedDetails = FormatEventDetails(jsonDoc, eventType);

        var htmlBody = $@"
            <html>
            <body>
                <h1>¡Nuevo Evento en el Mundial!</h1>
                <p>Se ha registrado un evento de tipo <b>{eventType}</b> en el Partido <b>{matchId}</b>.</p>
                <p>Detalles del Evento:</p>
                {formattedDetails}
                <p>Saludos,</p>
                <p>Tu Equipo de Alertas del Mundial</p>
            </body>
            </html>";

        // 2. CREACIÓN DEL CORREO: Usamos variable 'emailMessage' para ser claros
        var emailMessage = new EmailMessage();
        emailMessage.From = "onboarding@resend.dev"; // Obligatorio en modo gratis

        // 3. SOLUCIÓN ERROR LISTA: Resend.EmailAddressList se llena con .Add o inicializador
        emailMessage.To.Add("i2310354@continental.edu.pe");

        emailMessage.Subject = subject;
        emailMessage.HtmlBody = htmlBody;

        try
        {
            _logger.LogInformation("EMAIL-SERVICE: Intentando enviar correo a {To}", emailMessage.To[0]);

            // 4. SOLUCIÓN RESPUESTA: La librería suele devolver el ID directamente o lanzar Excepción si falla.
            // No intentamos leer .StatusCode porque la librería abstrae eso.
            var response = await _resend.EmailSendAsync(emailMessage);

            // Si llegamos a esta línea, es que funcionó.
            // Dependiendo de la versión de la librería, 'response' puede ser el Guid (Id) o un objeto.
            // Asumiremos que si no falló, todo está bien.

            _logger.LogInformation("EMAIL-SERVICE: Correo enviado con éxito.");
        }
        catch (Exception ex)
        {
            // Aquí capturamos si Resend dice "Error" (400, 500, etc)
            _logger.LogError(ex, "EMAIL-SERVICE: Error al enviar correo vía Resend.");
        }
    }

    private string FormatEventDetails(JsonDocument jsonDoc, Shared.Contracts.Enums.EventType eventType)
    {
        var root = jsonDoc.RootElement;
        var details = new List<string>();

        // Declarar variables JsonElement una vez para evitar conflictos de ámbito
        JsonElement element;

        // Siempre añadir MatchId si está presente
        if (root.TryGetProperty("MatchId", out element) && element.ValueKind != JsonValueKind.Null)
        {
            details.Add($"<b>ID de Partido:</b> {element.GetInt32()}");
        }

        switch (eventType)
        {
            case Shared.Contracts.Enums.EventType.MatchStarted:
                if (root.TryGetProperty("HomeTeamName", out element))
                    details.Add($"<b>Equipo Local:</b> {element.GetString()}");
                if (root.TryGetProperty("AwayTeamName", out element))
                    details.Add($"<b>Equipo Visitante:</b> {element.GetString()}");
                break;
            case Shared.Contracts.Enums.EventType.MatchEnded:
                if (root.TryGetProperty("FinalHomeScore", out element))
                    details.Add($"<b>Marcador Final Local:</b> {element.GetInt32()}");
                if (root.TryGetProperty("FinalAwayScore", out element))
                    details.Add($"<b>Marcador Final Visitante:</b> {element.GetInt32()}");
                break;
            case Shared.Contracts.Enums.EventType.Goal:
                if (root.TryGetProperty("Minute", out element))
                    details.Add($"<b>Minuto:</b> {element.GetInt32()}");
                if (root.TryGetProperty("TeamId", out element))
                    details.Add($"<b>ID de Equipo:</b> {element.GetInt32()}");
                if (root.TryGetProperty("PlayerId", out element))
                    details.Add($"<b>ID de Jugador:</b> {element.GetInt32()}");
                if (root.TryGetProperty("NewHomeScore", out element))
                    details.Add($"<b>Nuevo Marcador Local:</b> {element.GetInt32()}");
                if (root.TryGetProperty("NewAwayScore", out element))
                    details.Add($"<b>Nuevo Marcador Visitante:</b> {element.GetInt32()}");
                break;
            case Shared.Contracts.Enums.EventType.Card:
                if (root.TryGetProperty("Minute", out element))
                    details.Add($"<b>Minuto:</b> {element.GetInt32()}");
                if (root.TryGetProperty("TeamId", out element))
                    details.Add($"<b>ID de Equipo:</b> {element.GetInt32()}");
                if (root.TryGetProperty("PlayerId", out element))
                    details.Add($"<b>ID de Jugador:</b> {element.GetInt32()}");
                if (root.TryGetProperty("CardType", out element))
                {
                    var cardType = (Shared.Contracts.Events.CardType)element.GetInt32();
                    details.Add($"<b>Tipo de Tarjeta:</b> {cardType}");
                }
                break;
            case Shared.Contracts.Enums.EventType.Substitution:
                if (root.TryGetProperty("Minute", out element))
                    details.Add($"<b>Minuto:</b> {element.GetInt32()}");
                if (root.TryGetProperty("TeamId", out element))
                    details.Add($"<b>ID de Equipo:</b> {element.GetInt32()}");
                if (root.TryGetProperty("PlayerInId", out element))
                    details.Add($"<b>Jugador Entra (ID):</b> {element.GetInt32()}");
                if (root.TryGetProperty("PlayerOutId", out element))
                    details.Add($"<b>Jugador Sale (ID):</b> {element.GetInt32()}");
                break;
            default:
                details.Add($"<b>JSON Original:</b> <pre>{root.GetRawText()}</pre>");
                break;
        }

        return string.Join("<br/>", details);
    }
}