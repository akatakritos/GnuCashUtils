using Microsoft.Data.Sqlite;

namespace GnuCashUtils.BulkEdit;

public interface IDbConnectionFactory
{
    public SqliteConnection GetConnection();
}

public class SqliteConnectionFactory : IDbConnectionFactory
{
    public SqliteConnection GetConnection()
    {
        return new SqliteConnection("Data Source=/Users/mattburke/personal-copy.sqlite.gnucash");
    }
}