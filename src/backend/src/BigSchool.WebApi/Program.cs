using Autofac;
using Autofac.Extensions.DependencyInjection;
using BigSchool.Application.Configuration;
using BigSchool.Application.Infrastructure;
using BigSchool.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/bigschool-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console()
        .WriteTo.File("logs/bigschool-.log", rollingInterval: RollingInterval.Day));

    // AppSettings binding
    builder.Services.Configure<AppSettings>(options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
        options.Jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
        options.RagService = builder.Configuration.GetSection("RagService").Get<RagServiceSettings>()
            ?? new RagServiceSettings { BaseUrl = "http://localhost:8000" };
    });

    // Autofac como DI container (simplified: one RegisterAssemblyTypes per layer)
    builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
    builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
    {
        // Domain layer (excluir entidades — solo servicios de dominio)
        containerBuilder.RegisterAssemblyTypes(typeof(BigSchool.Domain.Entities.BaseEntity).Assembly)
            .Where(t => !t.Namespace!.Contains("Entities") && !t.Namespace!.Contains("Enums") && !t.Namespace!.Contains("Exceptions"))
            .AsImplementedInterfaces();

        // Application layer
        containerBuilder.RegisterAssemblyTypes(typeof(AppSettings).Assembly)
            .AsImplementedInterfaces();

        // Infrastructure layer
        containerBuilder.RegisterAssemblyTypes(typeof(BigSchoolDbContext).Assembly)
            .AsImplementedInterfaces();

        // WebApi layer
        containerBuilder.RegisterAssemblyTypes(typeof(Program).Assembly)
            .AsImplementedInterfaces();

        // DbContext
        containerBuilder.Register(ctx =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<BigSchoolDbContext>();
            var configuration = ctx.Resolve<Microsoft.Extensions.Configuration.IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            optionsBuilder.UseMySql(connectionString!, ServerVersion.AutoDetect(connectionString!));
            var mediator = ctx.Resolve<IMediator>();
            return new BigSchoolDbContext(optionsBuilder.Options, mediator);
        })
        .AsSelf()
        .As<BigSchool.Domain.Interfaces.IUnitOfWork>()
        .InstancePerLifetimeScope();
    });

    // MediatR con CustomMediatR (dispatch secuencial por defecto: SyncContinueOnException)
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(AppSettings).Assembly);
        cfg.AddOpenBehavior(typeof(BigSchool.Application.Behaviors.ValidationBehavior<,>));
    });
    builder.Services.AddTransient<IMediator, CustomMediatR>();

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "BigSchool API",
            Version = "v1",
            Description = "API de finanzas personales e inversiones"
        });
    });

    // Controllers
    builder.Services.AddControllers();

    // Health checks
    builder.Services.AddHealthChecks();

    // CORS (desarrollo)
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        });
    });

    // Data Protection (para encrypt de userId en JWT)
    builder.Services.AddDataProtection();

    // JWT Authentication
    var jwtSecret = builder.Configuration["Jwt:Secret"]!;
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtSecret))
        };
    });
    builder.Services.AddAuthorization();

    var app = builder.Build();

    // Middleware pipeline
    app.UseMiddleware<BigSchool.WebApi.Middleware.ExceptionHandlingMiddleware>();
    
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "BigSchool API v1"));
    }

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Necesario para Integration Tests (WebApplicationFactory)
public partial class Program { }
