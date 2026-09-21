using BookStoreApi.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BookStoreApi.Services;

public class BooksService(IOptions<BookStoreDatabaseSettings> bookStoreDatabaseSettings)
{
    private readonly IMongoCollection<Book> _booksCollection = new MongoClient(bookStoreDatabaseSettings.Value.ConnectionString)
        .GetDatabase(bookStoreDatabaseSettings.Value.DatabaseName)
        .GetCollection<Book>(bookStoreDatabaseSettings.Value.BooksCollectionName);

    public async Task<List<Book>> GetAsync() =>
        await _booksCollection.Find(_ => true).ToListAsync();

    public async Task<Book?> GetAsync(string id) =>
        await _booksCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task CreateAsync(Book newBook) =>
        await _booksCollection.InsertOneAsync(newBook);

    public async Task UpdateAsync(string id, Book updatedBook) =>
        await _booksCollection.ReplaceOneAsync(x => x.Id == id, updatedBook);

    public async Task RemoveAsync(string id) =>
        await _booksCollection.DeleteOneAsync(x => x.Id == id);

    public async Task RemoveAll() =>
        await _booksCollection.DeleteManyAsync(Builders<Book>.Filter.Empty);

    public async Task SeedDefaultBooksAsync()
    {
        var count = await _booksCollection.CountDocumentsAsync(Builders<Book>.Filter.Empty);
        if (count == 0)
        {
            var initialBooks = new List<Book>
            {
                new() { BookName = "Design Patterns: Elements of Reusable Object-Oriented Software", Price = 54.93M, Category = "Computers", Author = "Ralph Johnson, Erich Gamma, John Vlissides, Richard Helm" },
                new() { BookName = "Clean Code: A Handbook of Agile Software Craftsmanship", Price = 43.15M, Category = "Computers", Author = "Robert C. Martin" },
                new() { BookName = "Refactoring: Improving the Design of Existing Code", Price = 47.99M, Category = "Computers", Author = "Martin Fowler" },
                new() { BookName = "Designing Data-Intensive Applications", Price = 58.50M, Category = "Databases", Author = "Martin Kleppmann" },
                new() { BookName = "Building Microservices: Designing Fine-Grained Systems", Price = 52.00M, Category = "Architecture", Author = "Sam Newman" },
                new() { BookName = "Site Reliability Engineering", Price = 49.95M, Category = "DevOps", Author = "Betsy Beyer, Chris Jones, Jennifer Petoff" }
            };
            await _booksCollection.InsertManyAsync(initialBooks);
        }
    }
}