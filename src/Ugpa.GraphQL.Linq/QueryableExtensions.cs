using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Ugpa.GraphQL.Linq
{
    public static class QueryableExtensions
    {
        public static IQueryable<T> Where<T, TParams>(this IQueryable<T> source, TParams @params)
        {
            return source.Provider.CreateQuery<T>(Expression.Call(
                null,
                ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(T), typeof(TParams)),
                new Expression[] { source.Expression, Expression.Constant(@params) }));
        }
    }
}
