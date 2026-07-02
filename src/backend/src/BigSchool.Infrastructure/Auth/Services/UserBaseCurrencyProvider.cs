using BigSchool.Application.Auth.Interfaces.Repositories;
using BigSchool.Application.SharedKernel.Interfaces.Services;
using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Infrastructure.Auth.Services;

public sealed class UserBaseCurrencyProvider : IUserBaseCurrencyProvider
{
    private readonly IUserRepository _userRepository;

    public UserBaseCurrencyProvider(IUserRepository userRepository) => _userRepository = userRepository;

    public async Task<Currency?> GetBaseCurrencyAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user?.BaseCurrency;
    }
}
