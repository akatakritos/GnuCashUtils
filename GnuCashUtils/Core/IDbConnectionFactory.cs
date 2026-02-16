using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace GnuCashUtils.Core;

public interface IDbConnectionFactory
{
    public SqliteConnection GetConnection();
    public void SetDatabase(string path);
}

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private string _connectionString = "";
    public SqliteConnection GetConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public void SetDatabase(string path)
    {
        if (!File.Exists(path)) throw new Exception("Database file does not exist");
        _connectionString = $"Data Source={path}";
    }
}