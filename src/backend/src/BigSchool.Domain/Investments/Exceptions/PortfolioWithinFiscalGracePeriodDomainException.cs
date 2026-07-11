using BigSchool.Domain.SharedKernel.Exceptions;

namespace BigSchool.Domain.Investments.Exceptions;

public class PortfolioWithinFiscalGracePeriodDomainException : ConflictException
{
    public PortfolioWithinFiscalGracePeriodDomainException(int idPortfolio, DateOnly lastSaleDate)
        : base("PORTFOLIO_WITHIN_FISCAL_GRACE",
            $"La cartera {idPortfolio} no puede borrarse hasta 5 años desde la última venta ({lastSaleDate:yyyy-MM-dd}).") { }
}
