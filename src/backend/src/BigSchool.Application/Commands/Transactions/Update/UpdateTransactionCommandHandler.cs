using BigSchool.Application.DTOs.Transactions;
using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.ValueObjects;
using MediatR;

namespace BigSchool.Application.Commands.Transactions.Update;

public class UpdateTransactionCommandHandler : IRequestHandler<UpdateTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IExchangeRateProvider _exchangeRateProvider;

    public UpdateTransactionCommandHandler(
        ITransactionRepository transactionRepository,
        IUserRepository userRepository,
        IExchangeRateProvider exchangeRateProvider)
    {
        _transactionRepository = transactionRepository;
        _userRepository = userRepository;
        _exchangeRateProvider = exchangeRateProvider;
    }

    public async Task<TransactionDto> Handle(UpdateTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _transactionRepository.GetByIdAsync(request.IdTransaction, cancellationToken);
        if (transaction is null || transaction.IdUser != request.IdUser)
            throw new NotFoundException(nameof(Transaction), request.IdTransaction);

        var user = await _userRepository.GetByIdAsync(request.IdUser, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.IdUser);

        var currency = request.Currency ?? user.BaseCurrency;
        var rate = currency == user.BaseCurrency
            ? 1m
            : await _exchangeRateProvider.GetRateAsync(currency, user.BaseCurrency, request.TransactionDate, cancellationToken);

        transaction.Update(
            request.Type, request.IdMainCategory, request.IdSubCategory, request.Description,
            Money.Create(request.Amount, currency), user.BaseCurrency, rate,
            request.TransactionDate, request.TransactionDate);

        await _transactionRepository.UnitOfWork.SaveChangesAsync();

        var c = transaction.Conversion;
        return new TransactionDto(
            transaction.IdTransaction, transaction.Type, transaction.IdMainCategory,
            transaction.IdSubCategory, transaction.Description, transaction.TransactionDate,
            c.Original.Amount, c.Original.Currency, c.Rate, c.Base.Amount, c.Base.Currency, c.RateDate);
    }
}
