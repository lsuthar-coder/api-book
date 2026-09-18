using BookStoreApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BookStoreApi.Services;

public class UsersService(
    IOptions<BookStoreDatabaseSettings> databaseSettings,
    PasswordHasher<User> passwordHasher)
{
    private readonly IMongoCollection<User> _users = new MongoClient(databaseSettings.Value.ConnectionString)
        .GetDatabase(databaseSettings.Value.DatabaseName)
        .GetCollection<User>(databaseSettings.Value.UsersCollectionName);

    public async Task<User?> GetByEmailAsync(string email) =>
        await _users.Find(user => user.Email == email.Trim().ToLowerInvariant()).FirstOrDefaultAsync();

    public async Task<User> CreateAsync(string name, string email, string password)
    {
        var user = new User
        {
            Name = name.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Role = "user"
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        await _users.InsertOneAsync(user);
        return user;
    }

    public async Task<bool> PromoteToAdminAsync(string email)
    {
        var result = await _users.UpdateOneAsync(
            user => user.Email == email.Trim().ToLowerInvariant(),
            Builders<User>.Update.Set(user => user.Role, "admin"));
        return result.MatchedCount > 0;
    }

    public bool VerifyPassword(User user, string password) =>
        passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}