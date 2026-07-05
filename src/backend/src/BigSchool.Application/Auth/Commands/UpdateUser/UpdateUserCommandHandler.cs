using BigSchool.Application.Auth.DTOs;
using BigSchool.Application.Auth.Interfaces.Repositories;
using BigSchool.Application.Auth.Interfaces.Services;
using BigSchool.Domain.SharedKernel.Exceptions;
using MediatR;

namespace BigSchool.Application.Auth.Commands.UpdateUser;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserProfileDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public UpdateUserCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserProfileDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.IdUser, cancellationToken)
            ?? throw new NotFoundException("User", request.IdUser);

        user.UpdateProfile(request.FullName);
        if (request.Password is not null)
        {
            var (hash, salt) = _passwordHasher.HashPassword(request.Password);
            user.ChangePassword(hash, salt);
        }

        await _userRepository.UnitOfWork.SaveChangesAsync();
        return new UserProfileDto(user.IdUser, user.Email, user.FullName,
            user.BaseCurrency.ToString(), user.LastLoginDate);
    }
}
