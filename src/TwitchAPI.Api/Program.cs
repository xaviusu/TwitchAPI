using Microsoft.AspNetCore.Mvc;
using TwitchAPI.Api.Contracts;
using TwitchAPI.Api.ErrorHandling;
using TwitchAPI.Api.ModelBinding;
using TwitchAPI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
        options.ModelBinderProviders.Insert(0, new TwitchUserIdModelBinderProvider()))
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(ErrorResponse.InvalidId));
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<TwitchUnauthorizedExceptionHandler>();
builder.Services.AddExceptionHandler<InternalServerErrorExceptionHandler>();
builder.Services.AddTwitchInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseRegisteredExceptionHandlers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
