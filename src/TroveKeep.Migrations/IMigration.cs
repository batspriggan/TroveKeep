using MongoDB.Driver;

namespace TroveKeep.Migrations;

public interface IMigration
{
    int VersionFrom { get; }
    int VersionTo { get; }
    string Description {get;}
    Task RunAsync(IMongoDatabase database);
}
