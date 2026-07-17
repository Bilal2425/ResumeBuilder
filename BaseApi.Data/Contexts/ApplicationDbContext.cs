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
            var connectionString = options.Value.ConnectionString ?? "mongodb://localhost:27017/";
            var databaseName = options.Value.DatabaseName ?? "resumeBuilderDb";

            // Creating a MongoClient and get DB info
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
        }

        public IMongoCollection<Resume> Resumes => _database.GetCollection<Resume>("Resumes");
        public IMongoCollection<Template> Templates => _database.GetCollection<Template>("Templates");
    }
}
