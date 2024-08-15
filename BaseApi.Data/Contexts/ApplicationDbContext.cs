using BaseApi.Model;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BaseApi.Data.Contexts
{

    public class MongoDbSettings
    {
        public string? ConnectionString { get; set; }
        public string? DatabaseName { get; set; }
    }

    public class ApplicationDbContext
    {
        private readonly IMongoDatabase _database;

        public ApplicationDbContext(IOptions<MongoDbSettings> options)
        {
            // Creating a MongoClient and get DB info
            var client = new MongoClient(options.Value.ConnectionString);
            _database = client.GetDatabase(options.Value.DatabaseName);
        }

        public IMongoCollection<Resume> Resumes => _database.GetCollection<Resume>("Resumes");
    }
}
