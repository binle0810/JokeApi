using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Application.Behaviors;
using Domain.Abstractions;
using FluentValidation;
using Infrastructure;
using Infrastructure.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using JokeApi.Middleware;
using Application.Abstractions;
using Application.ChuckNorris.Queries.GetRandomJokeBySearch;
using Infrastructure.Authentication;
using Domain.Entities;
using Hangfire;
using Hangfire.PostgreSql;
using Infrastructure.Service;
using Application.Email.Command;
using static System.Net.Mime.MediaTypeNames;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình dịch vụ (tương đương ConfigureServices)
var presentationAssembly = typeof(Presentation.AssemblyReference).Assembly;
builder.Services.AddControllers()
    .AddApplicationPart(presentationAssembly);

var applicationAssembly = typeof(Application.AssemblyReference).Assembly;
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddValidatorsFromAssembly(applicationAssembly);

// Cấu hình Swagger
builder.Services.AddSwaggerGen(c =>
{
    var presentationDocumentationFile = $"{presentationAssembly.GetName().Name}.xml";
    var presentationDocumentationFilePath = Path.Combine(AppContext.BaseDirectory, presentationDocumentationFile);
    c.IncludeXmlComments(presentationDocumentationFilePath);
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Web API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter a valid token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Cấu hình DbContext với PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Application")));

// Đăng ký các dịch vụ scoped
builder.Services.AddScoped<IWebinarRepository, WebinarRepository>();
builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IDbConnection>(sp => sp.GetRequiredService<ApplicationDbContext>().Database.GetDbConnection());

// Middleware xử lý ngoại lệ
builder.Services.AddTransient<ExceptionHandlingMiddleware>();

// Cấu hình Chuck Norris API Service
builder.Services.AddHttpClient<IChuckNorrisService, ChuckNorrisService>(client =>
{
    client.BaseAddress = new Uri("https://api.chucknorris.io/");
});

// Cấu hình JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtOptions>();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
}).AddEntityFrameworkStores<ApplicationDbContext>()
  .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignOutScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
    };
});

// Đăng ký các dịch vụ authentication và cache
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICacheService, CacheService>();

// Cấu hình Hangfire với PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("PostgreSqlConnection");
builder.Services.AddHangfire(config => config.UsePostgreSqlStorage(options =>
{
    options.UseNpgsqlConnection(connectionString);
}, new PostgreSqlStorageOptions { SchemaName = "hangfire" }));
builder.Services.AddHangfireServer();

// Cấu hình Email và Memory Cache
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddMemoryCache();

// Xây dựng ứng dụng
var app = builder.Build();

// Áp dụng migrations
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);


// Cấu hình middleware pipeline (tương đương Configure)
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Web API v1"));
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire");

app.UseEndpoints(endpoints => endpoints.MapControllers());

// Chạy ứng dụng
await app.RunAsync();
