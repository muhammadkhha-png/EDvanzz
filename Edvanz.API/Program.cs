using Edvanz.Application.Extensions;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure;
using Edvanz.Infrastructure.Persistence;
using Edvanz.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using System;
using System.Globalization;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDbContext<EdvanzDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("con")));
builder.Services.AddScoped<IUserRepo, UserRepo>();
builder.Services.AddScoped(typeof(IUnitOfWork), typeof(UnitOfWork));


builder.Services.AddApplication();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });


});
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Edvanz", Version = "v1" });


});
var app = builder.Build();

// Use localization middleware
var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);
app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
    app.UseSwaggerUI(
        c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", "Edvanz v1");
        });


app.UseHttpsRedirection();
app.UseCors("AllowAll"); 

app.UseAuthorization();

app.MapControllers();

app.Run();
