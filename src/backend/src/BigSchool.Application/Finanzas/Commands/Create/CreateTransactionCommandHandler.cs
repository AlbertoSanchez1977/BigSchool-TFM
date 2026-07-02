using BigSchool.Application.Finanzas.DTOs;
using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Domain.SharedKernel.Exceptions;
using BigSchool.Domain.SharedKernel.ValueObjects;
using MediatR;
using BigSchool.Application.Finanzas.Interfaces.Repositories;
using BigSchool.Application.SharedKernel.Interfaces.Services;

namespace BigSchool.Application.Finanzas.Commands.Create;

public class CreateTransactionCommandHandler : IRequestHandler<CreateTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUserBaseCurrencyProvider _userBaseCurrencyProvider;
    private readonly IExchangeRateProvider _exchangeRateProvider;

    public CreateTransactionCommandHandler(
        ITransactionRepository transactionRepository,
        IUserBaseCurrencyProvider userBaseCurrencyProvider,
        IExchangeRateProvider exchangeRateProvider)
    {
        _transactionRepository = transactionRepository;
        _userBaseCurrencyProvider = userBaseCurrencyProvider;
        _exchangeRateProvider = exchangeRateProvider;
    }

    public async Task<TransactionDto> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        var userBaseCurrency = await _userBaseCurrencyProvider.GetBaseCurrencyAsync(request.IdUser, cancellationToken)
            ?? throw new NotFoundException("User", request.IdUser);

        var currency = request.Currency ?? userBaseCurrency;
        var rate = currency == userBaseCurrency
            ? 1m
            : await _exchangeRateProvider.GetRateAsync(currency, userBaseCurrency, request.TransactionDate, cancellationToken);

        var transaction = Transaction.Create(
            request.IdUser,
            request.Type,
            request.IdMainCategory,
            request.IdSubCategory,
            request.Description,
            Money.Create(request.Amount, currency),
            userBaseCurrency,
            rate,
            request.TransactionDate,
            request.TransactionDate);

        await _transactionRepository.AddAsync(transaction, cancellationToken);
        await _transactionRepository.UnitOfWork.SaveChangesAsync();

        var c = transaction.Conversion;
        return new TransactionDto(
            transaction.IdTransaction, transaction.Type, transaction.IdMainCategory,
            transaction.IdSubCategory, transaction.Description, transaction.TransactionDate,
            c.Original.Amount, c.Original.Currency, c.Rate, c.Base.Amount, c.Base.Currency, c.RateDate);
    }
}
