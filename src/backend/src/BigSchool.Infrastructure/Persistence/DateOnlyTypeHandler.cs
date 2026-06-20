using System.Data;
using Dapper;

namespace BigSchool.Infrastructure.Persistence;

public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
        => parameter.Value = value.ToDateTime(TimeOnly.MinValue);

    public override DateOnly Parse(object value)
        => DateOnly.FromDateTime(Convert.ToDateTime(value));
}
