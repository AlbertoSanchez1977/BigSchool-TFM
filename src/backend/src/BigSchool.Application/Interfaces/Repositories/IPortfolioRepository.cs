using BigSchool.Domain.Investments.Entities;

namespace BigSchool.Application.Interfaces.Repositories;

public interface IPortfolioRepository : IRepository<Portfolio, int>
{
    /// <summary>Carga la cartera con sus Holdings y los Disposals de cada uno (grafo completo del agregado) para operaciones de escritura (AddHolding, SellShares, Update/Delete).</summary>
    Task<Portfolio?> GetByIdWithHoldingsAsync(int id, CancellationToken cancellationToken = default);
}
