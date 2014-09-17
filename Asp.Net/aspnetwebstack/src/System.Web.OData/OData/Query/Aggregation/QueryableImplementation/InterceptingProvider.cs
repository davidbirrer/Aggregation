﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;


namespace System.Web.OData.OData.Query.Aggregation.QueryableImplementation
{
    /// <summary>
    /// A query provider that wrap an <see cref="IQueryProvider"/> and allows visitors to wrap the query execution. 
    /// Also if the query is unsupported by the underlying query provider the <see cref="InterceptingProvider"/> will transform its queries to be executed on a memory collection.   
    /// </summary>
    public class InterceptingProvider : IQueryProvider
    {
        private IQueryProvider _underlyingProvider;
        private Func<Expression, Expression>[] _visitors;

        /// <summary>
        /// Gets or Sets a function that combines temporary results into one final result. 
        /// This function is used when the collection to query has more elements than allowed to query in a single transaction
        /// </summary>
        public Func<List<Tuple<object, int>>, object> Combiner { get; set; }

        /// <summary>
        /// Gets the max number of elements that is allowed to query in a single transaction
        /// </summary>
        public int MaxResults { get; private set; }

        private InterceptingProvider(
            IQueryProvider underlyingQueryProvider,
            params Func<Expression, Expression>[] visitors)
        {
            this._underlyingProvider = underlyingQueryProvider;
            this._visitors = visitors;
        }

        /// <summary>
        /// Create a new <see cref="InterceptingProvider"/> and set its visitors
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="underlyingQuery"></param>
        /// <param name="maxResults"></param>
        /// <param name="visitors"></param>
        /// <returns></returns>
        public static IQueryable<T> Intercept<T>(
            IQueryable<T> underlyingQuery, int maxResults,
            params ExpressionVisitor[] visitors)
        {
            Func<Expression, Expression>[] visitFuncs;
            if (visitors == null)
            {
                visitFuncs = null;
            }
            else
            {
                visitFuncs = visitors
                        .Select(v => (Func<Expression, Expression>)v.Visit)
                        .ToArray();
            }
            return Intercept<T>(underlyingQuery, maxResults, visitFuncs);
        }

        /// <summary>
        /// Create a new <see cref="InterceptingProvider"/> and set its visitors
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="underlyingQuery"></param>
        /// <param name="maxResults"></param>
        /// <param name="visitors"></param>
        /// <returns></returns>
        public static IQueryable<T> Intercept<T>(
            IQueryable<T> underlyingQuery, int maxResults,
            params Func<Expression, Expression>[] visitors)
        {
            InterceptingProvider provider = new InterceptingProvider(underlyingQuery.Provider, visitors)
            {
                MaxResults = maxResults
            };
            return provider.CreateQuery<T>(underlyingQuery.Expression);
        }

        /// <summary>
        /// Execute a an expression and return an enumerator as a result
        /// </summary>
        /// <typeparam name="TResult">Type of the result</typeparam>
        /// <param name="expression">The expression tree to execute</param>
        /// <returns>an enumerator of the result</returns>
        public IEnumerator<TElement> ExecuteQuery<TElement>(
            Expression expression)
        {
            return _underlyingProvider.CreateQuery<TElement>(InterceptExpr(expression)).GetEnumerator();
        }

        /// <inheritdoc />
        public IQueryable<TElement> CreateQuery<TElement>(
            Expression expression)
        {
            return new InterceptedQuery<TElement>(this, expression);
        }

        /// <inheritdoc />
        public IQueryable CreateQuery(Expression expression)
        {
            Type et = expression.Type.FindIEnumerable();
            Type qt = typeof(InterceptedQuery<>).MakeGenericType(et);
            object[] args = new object[] { this, expression };

            ConstructorInfo ci = qt.GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new Type[] {  
                typeof(InterceptingProvider), 
                typeof(Expression) 
            },
                null);

            return (IQueryable)ci.Invoke(args);
        }

        /// <summary>
        /// Execute the query. If it is not supported by the underlying query provider, transform it to be executed on a memory collection 
        /// </summary>
        /// <typeparam name="TResult">Type of the result</typeparam>
        /// <param name="expression">The expression tree to execute</param>
        /// <returns>The expression execution result</returns>
        public TResult Execute<TResult>(Expression expression)
        {
            try
            {
                return this._underlyingProvider.Execute<TResult>(InterceptExpr(expression));
            }
            catch (NotSupportedException ex)
            {
                var adapter = new QueriableProviderAdapter() { Provider = _underlyingProvider, MaxCollectionSize = MaxResults };
                var res = adapter.Eval<TResult>(expression, Combiner);
                return res;
            }
            
        }

        /// <summary>
        /// Execute the query. If it is not supported by the underlying query provider, transform it to be executed on a memory collection
        /// </summary>
        /// <param name="expression">The expression tree to execute</param>
        /// <returns>The expression execution result</returns>
        public object Execute(Expression expression)
        {
            try
            {
                return this._underlyingProvider.Execute(InterceptExpr(expression));
            }
            catch (NotSupportedException ex)
            {
                var adapter = new QueriableProviderAdapter() { Provider = _underlyingProvider, MaxCollectionSize = MaxResults };
                var res = adapter.Eval(expression, Combiner);
                return res;
            }
            
        }

        /// <summary>
        /// Decorate the expression tree with a set of visitors
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        private Expression InterceptExpr(Expression expression)
        {
            Expression exp = expression;
            if (_visitors == null)
            {
                return exp;
            }

            foreach (var visitor in _visitors)
                exp = visitor(exp);
            return exp;
        }
    }
}