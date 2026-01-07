using Checkers.Models;
using Checkers.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
namespace Checkers.Core
{
    public class AppDbContext
    {
        private readonly IMongoDatabase _database;
        public AppDbContext(IOptions<MongoDbSettings> settings)
        {
            var client = new MongoClient(settings.Value.ConnectionString);
            _database = client.GetDatabase(settings.Value.DatabaseName);
        }
        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
        public IMongoCollection<Game> Games => _database.GetCollection<Game>("Games");
    }
}