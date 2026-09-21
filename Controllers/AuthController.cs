using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BookStoreApi.Models;
using BookStoreApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace BookStoreApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UsersService usersService,
    BooksService booksService,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Name, email, and password are required." });
        }

        if (await usersService.GetByEmailAsync(request.Email) is not null)
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        var role = !string.IsNullOrWhiteSpace(request.Role) ? request.Role : "user";
        var user = await usersService.CreateAsync(request.Name, request.Email, request.Password, role);
        return StatusCode(StatusCodes.Status201Created, new { token = CreateToken(user), user = new UserResponse(user.Id, user.Name, user.Email, user.Role) });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await usersService.GetByEmailAsync(request.Email);
        if (user is null || !usersService.VerifyPassword(user, request.Password))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(new { token = CreateToken(user), user = new UserResponse(user.Id, user.Name, user.Email, user.Role) });
    }

    [HttpPost("seed")]
    [AllowAnonymous]
    public async Task<IActionResult> Seed()
    {
        await usersService.SeedDefaultUsersAsync();
        await booksService.SeedDefaultBooksAsync();
        return Ok(new { message = "Database seeded successfully with default users and tutorial books." });
    }

    [HttpPost("users/{email}/promote")]
    [Authorize(Policy = "admin")]
    public async Task<IActionResult> Promote(string email)
    {
        return await usersService.PromoteToAdminAsync(email)
            ? Ok(new { message = "User promoted to admin." })
            : NotFound(new { message = "User not found." });
    }

    private string CreateToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(configuration.GetValue("Jwt:ExpiresInMinutes", 60));
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims,
            expires: expires, signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed record RegisterRequest(string Name, string Email, string Password, string? Role = "user");

public sealed record LoginRequest(string Email, string Password);

public sealed record UserResponse(string? Id, string Name, string Email, string Role);