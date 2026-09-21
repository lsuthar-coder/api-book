using BookStoreApi.Models;
using BookStoreApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("user", policy => policy.RequireRole("user", "admin"))
    .AddPolicy("admin", policy => policy.RequireRole("admin"));

builder.Services.Configure<BookStoreDatabaseSettings>(
    builder.Configuration.GetSection("BookStoreDatabaseConfiguration"));

builder.Services.AddOpenApi();

builder.Services.AddSingleton<BooksService>();
builder.Services.AddSingleton<UsersService>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.PasswordHasher<User>>();

builder.Services.AddControllers();

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
});
 
var app = builder.Build();

// Seed default users and sample books on startup
try
{
    var usersService = app.Services.GetRequiredService<UsersService>();
    var booksService = app.Services.GetRequiredService<BooksService>();
    await usersService.SeedDefaultUsersAsync();
    await booksService.SeedDefaultBooksAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"[Warning] Seed data skipped or failed: {ex.Message}");
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi().RequireAuthorization("user");
app.MapControllers();

// Reusable method to register Book endpoints for both "/books" and "/api/books"
void MapBookEndpoints(RouteGroupBuilder group)
{
    group.MapGet("/", async (BooksService booksService) =>
    {
        var books = await booksService.GetAsync();
        return Results.Ok(new { booksCount = books.Count, data = books });
    }).RequireAuthorization("user");

    group.MapGet("/{id:length(24)}", async (string id, BooksService booksService) =>
    {
        var book = await booksService.GetAsync(id);
        return book is null ? Results.NotFound() : Results.Ok(book);
    }).RequireAuthorization("user");

    group.MapPost("/", async (Book newBook, BooksService booksService) =>
    {
        await booksService.CreateAsync(newBook);
        return Results.Created($"/books/{newBook.Id}", newBook);
    }).RequireAuthorization("admin");

    group.MapPut("/{id:length(24)}", async (string id, Book updatedBook, BooksService booksService) =>
    {
        var book = await booksService.GetAsync(id);

        if (book is null)
        {
            return Results.NotFound();
        }

        updatedBook.Id = book.Id;

        await booksService.UpdateAsync(id, updatedBook);
        var updated = await booksService.GetAsync(id);
        return Results.Ok(updated);
    }).RequireAuthorization("admin");

    group.MapDelete("/{id:length(24)}", async (string id, BooksService booksService) =>
    {
        var book = await booksService.GetAsync(id);

        if (book is null)
        {
            return Results.NotFound();
        }

        await booksService.RemoveAsync(id);

        return Results.NoContent();
    }).RequireAuthorization("admin");

    group.MapDelete("/all", async (BooksService booksService) =>
    {
        await booksService.RemoveAll();
        return Results.Json(new { message = "All books removed" }, statusCode: StatusCodes.Status200OK);
    }).RequireAuthorization("admin");
}

MapBookEndpoints(app.MapGroup("/books"));
MapBookEndpoints(app.MapGroup("/api/books"));

// Root & Health check endpoints - Allow Anonymous for connectivity probes
app.MapGet("/", () => Results.Json(new { message = "Welcome to the BookStore API!", status = "Running" }, statusCode: StatusCodes.Status200OK)).AllowAnonymous();
app.MapGet("/health", () => Results.Json(new { status = "Healthy", timestamp = DateTime.UtcNow }, statusCode: StatusCodes.Status200OK)).AllowAnonymous();
app.MapGet("/api/health", () => Results.Json(new { status = "Healthy", timestamp = DateTime.UtcNow }, statusCode: StatusCodes.Status200OK)).AllowAnonymous();

app.Run();