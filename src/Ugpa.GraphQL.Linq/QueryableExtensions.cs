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
        /// Includes additional data based on selector. Doesn't change source <see cref="IEnumerable{TSource}"/>.
        /// </summary>
        /// <typeparam name="TSource">Element type.</typeparam>
        /// <typeparam name="TResult">Additional data type.</typeparam>
        /// <param name="source">Source <see cref="IEnumerable{TSource}"/>.</param>
        /// <param name="selector">Additional data selector delegate.</param>
        /// <returns>Source instance of <see cref="IEnumerable{TSource}"/>.</returns>
        /// <remarks>
        /// This method used only for building GraphQL query. It doesn't change source <see cref="IEnumerable{TSource}"/>.
        /// Also this method is used by enumerable rewriter.
        /// </remarks>
        public static IEnumerable<TSource> Include<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
            where TResult : class
        {
            return source;
        }

        /// <summary>
        /// Declares query fragment.
        /// </summary>
        /// <typeparam name="TSource">Query element type.</typeparam>
        /// <typeparam name="TFrag">Fragment element type.</typeparam>
        /// <param name="source">Source <see cref="IQueryable{TSource}"/>.</param>
        /// <param name="fragmentName">Fragment name.</param>
        /// <param name="fragmentQuery">Function to configure fragment.</param>
        /// <returns>New instance of <see cref="IQueryable{TSource}"/> with configured fragment.</returns>
        public static IQueryable<TSource> UsingFragment<TSource, TFrag>(this IQueryable<TSource> source, string fragmentName, Func<IQueryable<TFrag>, IQueryable<TFrag>> fragmentQuery)
        {
            var fragmentExpression = Utils.FragmentFactory.CreateFragment(fragmentName, fragmentQuery);
            return source.UsingFragment(fragmentExpression);
        }

        /// <summary>
        /// Declares query fragment.
        /// </summary>
        /// <typeparam name="TSource">Query element type.</typeparam>
        /// <typeparam name="TFrag">Fragment element type.</typeparam>
        /// <param name="source">Source <see cref="IQueryable{TSource}"/>.</param>
        /// <param name="fragment">Fragment <see cref="IQueryable{TFrag}"/>.</param>
        /// <returns>New instance of <see cref="IQueryable{TSource}"/> with configured fragment.</returns>
        public static IQueryable<TSource> UsingFragment<TSource, TFrag>(this IQueryable<TSource> source, Expression<Func<IQueryable<TFrag>, object>> fragment)
        {
            return source.Provider.CreateQuery<TSource>(Expression.Call(
                null,
                ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(TSource), typeof(TFrag)),
                new Expression[] { source.Expression, fragment }));
        }

        /// <summary>
        /// Declares query fragment. Doesn't change source <see cref="IEnumerable{TSource}"/>.
        /// </summary>
        /// <typeparam name="TSource">Query element type.</typeparam>
        /// <typeparam name="TFrag">Fragment element type.</typeparam>
        /// <param name="source">Source <see cref="IEnumerable{TSource}"/>.</param>
        /// <param name="fragment">Fragment <see cref="IEnumerable{TFrag}"/>.</param>
        /// <returns>Source instance of <see cref="IEnumerable{TSource}"/>.</returns>
        public static IEnumerable<TSource> UsingFragment<TSource, TFrag>(this IEnumerable<TSource> source, Func<IQueryable<TFrag>, object> fragment)
        {
            return source;
        }
    }
}
