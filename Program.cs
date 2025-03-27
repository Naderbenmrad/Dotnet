using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MBDPC3_Dotnet.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(5294));

var app = builder.Build();

var users = new List<User>();

// Logging Middleware
app.Use(async (context, next) =>
{
    Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");
    await next();
    Console.WriteLine($"Response: {context.Response.StatusCode}");
});

// Authentication Middleware
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/users") && context.Request.Query["authenticated"] != "true")
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized");
        return;
    }
    await next();
});

// User Validation Middleware
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/users" && context.Request.Method == "POST")
    {
        string body = await new System.IO.StreamReader(context.Request.Body).ReadToEndAsync();
        try
        {
            var user = JsonSerializer.Deserialize<User>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (user == null || string.IsNullOrEmpty(user.Username) || string.IsNullOrEmpty(user.Email) || string.IsNullOrEmpty(user.Name) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(user.Email))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid User Data");
                return;
            }
            context.Items["user"] = user;
        }
        catch (JsonException)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid JSON");
            return;
        }
    }
    await next();
});

app.MapGet("/users", () => users);
app.MapGet("/users/{id:int}", (int id) => users.FirstOrDefault(u => u.Id == id) is User user ? Results.Ok(user) : Results.NotFound());

app.MapPost("/users", async (HttpContext context) =>
{
    var user = context.Items["user"] as User;
    if (user != null)
    {
        user.Id = users.Count + 1;
        users.Add(user);
        return Results.Created($"/users/{user.Id}", user);
    }
    return Results.BadRequest("User not provided or invalid.");
});

app.MapPut("/users/{id:int}", async (HttpContext context, int id) =>
{
    var existingUser = users.FirstOrDefault(u => u.Id == id);
    if (existingUser is null) return Results.NotFound();

    var updatedUser = context.Items["user"] as User;
    if (updatedUser != null)
    {
        existingUser.Username = updatedUser.Username;
        existingUser.Email = updatedUser.Email;
        existingUser.Name = updatedUser.Name;
        return Results.NoContent();
    }
    return Results.BadRequest("User not provided or invalid.");
});

app.MapDelete("/users/{id:int}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null) return Results.NotFound();
    users.Remove(user);
    return Results.NoContent();
});

app.MapGet("/test", () => "Hello from test endpoint!");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
