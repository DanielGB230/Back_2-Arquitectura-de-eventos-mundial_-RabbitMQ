using Microsoft.EntityFrameworkCore;
using Backend.Consumidor.Api.Data;
using Backend.Consumidor.Api.Hubs;
using Backend.Consumidor.Api.Messaging;
using Backend.Consumidor.Api.Messaging.Consumers;
using Backend.Consumidor.Api.Services;
using Resend; // Added for Resend integration
using System.Linq; // Added for LINQ operations in new endpoint
using Backend.Consumidor.Api.Data.Models; // Added to resolve CS0246 for MatchStatus
using Backend.Consumidor.Api.Endpoints; // Added for MatchStatsEndpoints

var builder = WebApplication.CreateBuilder(args);



// 1. Configurar EF Core con SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<MatchDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Configurar RabbitMQ
builder.Services.Configure<RabbitMqConfiguration>(builder.Configuration.GetSection("RabbitMq"));

// 3. Configurar Resend
builder.Services.AddOptions();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(o =>
{
    o.ApiToken = builder.Configuration.GetSection("Resend")["ApiKey"]!;
});
builder.Services.AddTransient<IResend, ResendClient>();

// 4. Registrar SignalR
builder.Services.AddSignalR();


// 4. Añadir y configurar CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .WithOrigins("http://localhost:4200", "null") // Ahora el frontend está en http://localhost:4200. "null" para archivos locales.
              .AllowCredentials();
    });
});

// 5. Registrar Servicios de Lógica de Negocio
builder.Services.AddScoped<IPersistenceService, PersistenceService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddScoped<INotificationsService, NotificationsService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IOddsService, OddsService>(); // New service for Risk Analyst


// 6. Registrar los consumidores como Hosted Services
builder.Services.AddHostedService<PersistenceConsumer>();
builder.Services.AddHostedService<StatisticsConsumer>();
builder.Services.AddHostedService<NotificationsConsumer>();
builder.Services.AddHostedService<EmailConsumer>();
builder.Services.AddHostedService<RiskAnalystConsumer>(); // New consumer for Risk Analyst


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Usar la política de CORS por defecto
app.UseCors();

// Mapear los endpoints de MatchStats
app.MapMatchStatsEndpoints();
app.MapOddsEndpoints();

app.MapHub<NotificationsHub>("/notificationsHub");

app.Run();
