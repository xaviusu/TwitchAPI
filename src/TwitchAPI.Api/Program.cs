
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using TwitchAPI.Api.Contracts;
using TwitchAPI.Api.ErrorHandling;
using TwitchAPI.Application.Interfaces;
using TwitchAPI.Application.Services;
using TwitchAPI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = _ =>
            new BadRequestObjectResult(
                new ApiErrorResponse("Invalid request."));
    });

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddScoped<
    ITwitchUserService,
    TwitchUserService>();

builder.Services.AddScoped<
    ITwitchStreamService,
    TwitchStreamService>();

builder.Services.AddInfrastructure(
    builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

app.UseStatusCodePages(async context =>
{
    HttpResponse response = context.HttpContext.Response;

    await response.WriteAsJsonAsync(
        new ApiErrorResponse(
            ReasonPhrases.GetReasonPhrase(response.StatusCode)),
        context.HttpContext.RequestAborted);
});

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

public partial class Program { }
