using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Ugpa.GraphQL.Linq
{
    /// <summary>
    /// Provides extension methods for <see cref="IQueryable{T}"/>.
    /// </summary>
    public static class QueryableExtensions
    {
        /// <summary>
        /// Applies given object as parameters source to source <see cref="IQueryable{T}"/>.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <typeparam name="TParams">Parameters source type.</typeparam>
        /// <param name="source">Source <see cref="IQueryable{T}"/>.</param>
        /// <param name="params">Parameters source.</param>
        /// <returns>New instance of <see cref="IQueryable{T}"/> with applied parameters.</returns>
        public static IQueryable<T> Where<T, TParams>(this IQueryable<T> source, TParams @params)
        {
            return source.Provider.CreateQuery<T>(Expression.Call(
                null,
                ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(T), typeof(TParams)),
                new Expression[] { source.Expression, Expression.Constant(@params) }));
        }

        /// <summary>
        /// Returns source <see cref="IEnumerable{T}"/> without any changes.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <typeparam name="TParams">Parameters source type.</typeparam>
        /// <param name="source">Source <see cref="IEnumerable{T}"/>.</param>
        /// <param name="params">Parameters source.</param>
        /// <returns>Source instance of <see cref="IEnumerable{T}"/>.</returns>
        /// <remarks>This is utility method for enumerable rewriter.</remarks>
        public static IEnumerable<T> Where<T, TParams>(this IEnumerable<T> source, TParams @params)
            => source;

        /// <summary>
        /// Includes additional data based on selector.
        /// </summary>
        /// <typeparam name="TSource">Element type.</typeparam>
        /// <typeparam name="TResult">Additional data type.</typeparam>
        /// <param name="source">Source <see cref="IQueryable{T}"/>.</param>
        /// <param name="selector">Additional data selector expression.</param>
        /// <returns>New instance of <see cref="IQueryable{T}"/> with included additional data.</returns>
        public static IQueryable<TSource> Include<TSource, TResult>(this IQueryable<TSource> source, Expression<Func<TSource, TResult>> selector)
            where TResult : class
        {
            return source.Provider.CreateQuery<TSource>(Expression.Call(
                null,
                ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(TSource), typeof(TResult)),
                new Expression[] { source.Expression, Expression.Quote(selector) }));
        }

        /// <summary>
        /// Includes additional data based on selector. Doesn't change source <see cref="IEnumerable{T}"/>.
        /// </summary>
        /// <typeparam name="TSource">Element type.</typeparam>
        /// <typeparam name="TResult">Additional data type.</typeparam>
        /// <param name="source">Source <see cref="IEnumerable{T}"/>.</param>
        /// <param name="selector">Additional data selector delegate.</param>
        /// <returns>Source instance of <see cref="IEnumerable{T}"/>.</returns>
        /// <remarks>
        /// This method used only for building GraphQL query. It doesn't change source <see cref="IEnumerable{T}"/>.
        /// Also this method is used by enumerable rewriter.
        /// </remarks>
        public static IEnumerable<TSource> Include<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
            where TResult : class
        {
            return source;
        }
    }
}
