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

    public async Task HandleEventAsync(string jsonEventString)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        using JsonDocument jsonDoc = JsonDocument.Parse(jsonEventString);
        var root = jsonDoc.RootElement;

        if (!root.TryGetProperty("EventType", out var eventTypeElement) || !eventTypeElement.TryGetByte(out var eventTypeByte))
        {
            _logger.LogWarning("EMAIL-SERVICE: No se pudo determinar el EventType del mensaje.");
            return;
        }

        var eventType = (Shared.Contracts.Enums.EventType)eventTypeByte;

        // CAMBIO: MatchId ahora es Guid (String en JSON)
        string matchIdDisplay = "Desconocido";
        if (root.TryGetProperty("MatchId", out var matchIdElem))
        {
            matchIdDisplay = matchIdElem.GetString() ?? "Desconocido";
        }

        var subject = $"Alerta de Evento - {eventType} en Partido {matchIdDisplay}";

        var formattedDetails = FormatEventDetails(jsonDoc, eventType);

        var htmlBody = $@"
            <html>
            <body>
                <h1>¡Nuevo Evento en el Mundial!</h1>
                <p>Se ha registrado un evento de tipo <b>{eventType}</b> en el Partido <b>{matchIdDisplay}</b>.</p>
                <p>Detalles del Evento:</p>
                {formattedDetails}
                <p>Saludos,</p>
                <p>Tu Equipo de Alertas del Mundial</p>
            </body>
            </html>";

        var emailMessage = new EmailMessage();
        emailMessage.From = "onboarding@resend.dev"; 
        emailMessage.To.Add("i2310354@continental.edu.pe");

        emailMessage.Subject = subject;
        emailMessage.HtmlBody = htmlBody;

        try
        {
            _logger.LogInformation("EMAIL-SERVICE: Intentando enviar correo para MatchId {MatchId}", matchIdDisplay);
            await _resend.EmailSendAsync(emailMessage);
            _logger.LogInformation("EMAIL-SERVICE: Correo enviado con éxito.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EMAIL-SERVICE: Error al enviar correo vía Resend.");
        }
    }

    private string FormatEventDetails(JsonDocument jsonDoc, Shared.Contracts.Enums.EventType eventType)
    {
        var root = jsonDoc.RootElement;
        var details = new List<string>();
        JsonElement element;

        // CAMBIO: MatchId es String/Guid
        if (root.TryGetProperty("MatchId", out element) && element.ValueKind != JsonValueKind.Null)
        {
            details.Add($"<b>ID de Partido:</b> {element.GetString()}");
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
