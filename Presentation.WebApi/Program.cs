using Core.Application.Interfaces;
using FluentValidation;
using Infrastructure.Communication;
using Infrastructure.Cryptography;
using Infrastructure.Storage;
using Infrastructure.XmlServices;
using Infrastructure.Zip;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; 
using System.Globalization;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 2. CONFIGURACIÓN DE SWAGGER CON SOPORTE JWT
// ============================================================
builder.Services.AddEndpointsApiExplorer();

// 🎯 SOLUCIÓN AL ERROR: Esta es la instrucción clave que faltaba registrar
builder.Services.AddSwaggerGen(c =>
{

    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SUNAT Billing Microservice", Version = "v1" });

    // Configurar la caja de texto para pegar el token Bearer
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Autenticación JWT usando el esquema Bearer. Ejemplo: 'Bearer 12345abcdef'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});



// ============================================================
// 1. REGISTRO DE SEGURIDAD JWT
// ============================================================
var secretKey = Encoding.UTF8.GetBytes("ClaveSecretaSuperSeguraDe32Bytes"); // Usar Environment Variables en Prod
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = "BillingServiceEmisor",
        ValidAudience = "BillingServiceClientes",
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ClockSkew = TimeSpan.Zero
    };
});

//builder.Services.Configure<RequestLocalizationOptions>(options =>
//{
//    CultureInfo cultureenUs = new CultureInfo("en-US");
//    cultureenUs.NumberFormat.NumberDecimalSeparator = ".";
//    CultureInfo cultureen = new CultureInfo("en");
//    cultureen.NumberFormat.NumberDecimalSeparator = ".";

//    var supportedCultures = new[] { cultureenUs, cultureen };

//    options.DefaultRequestCulture = new RequestCulture("en-US");
//    options.SupportedCultures = supportedCultures;
//    options.SupportedUICultures = supportedCultures;
//});
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;


builder.Services.AddAuthorization();

// ============================================================
// 2. INYECCIÓN DE DEPENDENCIAS DE ARQUITECTURA LIMPIA
// ============================================================

//// 🧠 Capa 2: Core.Application (MediatR y FluentValidation)
//var applicationAssembly = typeof(Core.Application.Features.Invoices.Commands.CreateInvoiceCommand).Assembly;

//// 🎯 SOLUCIÓN AL ERROR: Sintaxis limpia para registrar MediatR
//builder.Services.AddMediatR(cfg =>
//{
//    cfg.RegisterServicesFromAssembly(applicationAssembly);
//});

//builder.Services.AddValidatorsFromAssembly(applicationAssembly);



var assembly = typeof(Core.Application.Features.Invoices.Commands.CreateInvoiceCommand).Assembly;

builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(assembly);
    // Registra el comportamiento de validación en la tubería de MediatR
    //cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

// Registra automáticamente todos los AbstractValidator del proyecto
builder.Services.AddValidatorsFromAssembly(assembly);





// ⚙️ Capa 3: Infrastructure (Componentes Técnicos)
builder.Services.AddHttpClient<ISunatSoapClient, SunatSoapClient>(); // HttpClient optimizado para SOAP
builder.Services.AddTransient<IXmlGenerator, UblXmlGenerator>();
builder.Services.AddTransient<ICryptoSigner, W3cXmlSigner>();
builder.Services.AddTransient<IZipService, ZipService>();

builder.Services.AddTransient<IStorageService, CloudflareStorageService>();


//builder.Services.AddAuthorization();

builder.Services.AddCors(
    opt =>
    opt.AddPolicy(
        name: "mycors",
        builder =>
        {
            builder.WithOrigins("*");
        }
        )

    );

var app = builder.Build();

// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SUNAT Billing v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// 🎯 MAPEO DIRECTO EN PROGRAM.CS PARA FORZAR A SWAGGER
app.MapPost("/api/v1/billing/invoice", async (
    Core.Application.Features.Invoices.Commands.CreateInvoiceCommand command,
    MediatR.IMediator mediator,
    FluentValidation.IValidator<Core.Application.Features.Invoices.Commands.CreateInvoiceCommand> validator) =>
{
    var validationResult = await validator.ValidateAsync(command);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    var response = await mediator.Send(command);
    return response.Success ? Results.Ok(response) : Results.BadRequest(response);
})
.WithTags("Facturación Electrónica"); // Agrupa visualmente el endpoint

app.Run();
