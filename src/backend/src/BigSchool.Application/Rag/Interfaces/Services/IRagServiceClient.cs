namespace BigSchool.Application.Rag.Interfaces.Services;

public interface IRagServiceClient
{
    Task<string> ChatAsync(string message, int userId, CancellationToken cancellationToken = default);
}
