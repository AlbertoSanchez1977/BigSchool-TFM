using BigSchool.Domain.SharedKernel.Exceptions;

namespace BigSchool.Domain.Investments.Exceptions;

public class PortfolioHasOpenPositionsDomainException : ConflictException
{
    public PortfolioHasOpenPositionsDomainException(int idPortfolio)
        : base("PORTFOLIO_HAS_OPEN_POSITIONS",
            $"La cartera {idPortfolio} tiene posiciones abiertas y no puede borrarse.") { }
}
