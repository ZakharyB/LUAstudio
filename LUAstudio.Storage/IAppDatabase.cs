using Microsoft.Data.Sqlite;

namespace LUAstudio.Storage;

public interface IAppDatabase
{
    string DatabasePath { get; }

    SqliteConnection OpenConnection();
}
