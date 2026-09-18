using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace BookStoreApi.Models;

public class Book
{
    // Marks this property as the primary key (_id) in the MongoDB collection
    [BsonId]
    // Converts MongoDB's binary ObjectId into a clean C# string automatically
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    // Maps the C# 'BookName' property to a database field named "Name" in MongoDB
    [BsonElement("Name")]
    // Forces the Web API to use exactly "Name" (PascalCase) in the network JSON payload
    [JsonPropertyName("Name")]
    public string BookName { get; set; } = null!;


    public decimal Price { get; set; }

    public string Category { get; set; } = null!;

    public string Author { get; set; } = null!;
}