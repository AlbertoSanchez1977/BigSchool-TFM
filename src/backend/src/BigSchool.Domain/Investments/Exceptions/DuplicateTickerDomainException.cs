using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BigSchool.Domain.SharedKernel.Exceptions;

namespace BigSchool.Domain.Investments.Exceptions;
public class DuplicateTickerDomainException : ConflictException
{
    public DuplicateTickerDomainException(string ticker)
    : base("DUPLICATE_TICKER", $"Ya existe una empresa con el ticker '{ticker}'.") { }
}
