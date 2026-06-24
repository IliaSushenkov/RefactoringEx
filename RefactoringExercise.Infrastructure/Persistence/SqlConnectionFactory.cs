using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace RefactoringExercise.Infrastructure.Persistence;

public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly DatabaseOptions _options;

    public SqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
    }

    public SqlConnection CreateConnection() => new(_options.ConnectionString);
}
