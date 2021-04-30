using System;
using System.Collections.Generic;
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

        public static IEnumerable<T> Where<T, TParams>(this IEnumerable<T> source, TParams @params)
            => source;

        public static IQueryable<TSource> Include<TSource, TResult>(this IQueryable<TSource> source, Expression<Func<TSource, TResult>> selector)
            where TResult : class
        {
            return source.Provider.CreateQuery<TSource>(Expression.Call(
                null,
                ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(TSource), typeof(TResult)),
                new Expression[] { source.Expression, Expression.Quote(selector) }));
        }

        public static IEnumerable<TSource> Include<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
            where TResult : class
        {
            return source;
        }
    }
}
