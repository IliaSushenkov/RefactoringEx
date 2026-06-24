using Microsoft.Data.SqlClient;

namespace RefactoringExercise.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    SqlConnection CreateConnection();
}
