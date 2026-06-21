using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigSchool.Domain.Exceptions;
public class DuplicateValuationDomainException : ConflictException
{
    public DuplicateValuationDomainException(string ticker, DateOnly date)
    : base("DUPLICATE_VALUATION",
        $"Ya existe una valoración de '{ticker}' para la fecha {date:yyyy-MM-dd}.")
    { }
}
