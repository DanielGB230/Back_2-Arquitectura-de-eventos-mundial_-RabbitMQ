using Backend.Productor.Api.Messaging;
using Backend.Productor.Api.Endpoints;
using Backend.Productor.Api.Services;
using System.Reflection; // Added for Assembly
using System.IO; // Added for Path.Combine

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar la lectura de la sección RabbitMq de appsettings.json
builder.Services.Configure<RabbitMqConfiguration>(builder.Configuration.GetSection("RabbitMq"));

// 2. Registrar el productor de RabbitMQ como Singleton
builder.Services.AddSingleton<IRabbitMqProducer, RabbitMqProducer>();

// 3. Registrar el servicio de eventos
builder.Services.AddScoped<IMatchEventService, MatchEventService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Usar la política de CORS por defecto
app.UseCors();

// Endpoint de prueba para verificar que la API está en funcionamiento
app.MapGet("/", () => "Backend.Productor.Api is running.");

// Mapea los endpoints de eventos del mundial.
app.MapMatchEventsEndpoints();

app.Run();
