using System;
using System.Linq;
using System.Linq.Expressions;

namespace Ugpa.GraphQL.Linq.Utils
{
    /// <summary>
    /// Provides methods to create query framents.
    /// </summary>
    public static class FragmentFactory
    {
        /// <summary>
        /// Creates expression representing query fragment.
        /// </summary>
        /// <typeparam name="TFrag">Fragment element type.</typeparam>
        /// <param name="fragmentName">Fragment name.</param>
        /// <param name="fragmentQuery">Function to configure fragment.</param>
        /// <returns>New instance of expression representing query fragment.</returns>
        public static Expression<Func<IQueryable<TFrag>, object>> CreateFragment<TFrag>(string fragmentName, Func<IQueryable<TFrag>, IQueryable<TFrag>> fragmentQuery)
        {
            var parameter = Expression.Parameter(typeof(IQueryable<TFrag>), fragmentName);
            var fragmentSource = Enumerable.Empty<TFrag>().AsQueryable().Provider.CreateQuery<TFrag>(parameter);
            return Expression.Lambda<Func<IQueryable<TFrag>, object>>(fragmentQuery(fragmentSource).Expression, parameter);
        }
    }
}
