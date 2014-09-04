﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace System.Web.OData.OData.Query.Aggregation.QueryableImplementation
{
    /// <summary>
    /// Adapter that converts an expression tree that contain a function call which is not supported on a particular IQueruable provider to use an in memory implementation
    /// </summary>
    internal class QueriableProviderAdapter
    {
        /// <summary>
        /// Save the list of unsupported methods per provider
        /// </summary>
        private static ConcurrentDictionary<string, List<string>> UnsupportedMethodsPerProvider =
            new ConcurrentDictionary<string, List<string>>();

        public IQueryProvider Provider { get; set; }

        public int MaxCollectionSize { get; set; }

        /// <summary>
        /// Converts an expression tree that contain a function call which is not supported on a particular <see cref="IQueruable"/> provider to use an in memory implementation
        /// </summary>
        /// <typeparam name="TResult">The result of the method call expression</typeparam>
        /// <param name="query">The expression tree to convert</param>
        /// <param name="combinerOfTemporaryResults">A function that is used to combine temporary results to a final one. 
        /// This function is used when the query execution has to be split because the collection to query has more elements than 
        /// the max number of results allowed in a single transaction against a persistence provider.
        /// </param>
        /// <returns>A result of a method call expression</returns>
        public TResult Eval<TResult>(Expression query, Func<List<object>, object> combinerOfTemporaryResults = null)
        {
            var baseCollections = new Dictionary<Expression, QueryableRecord>();
            var tempResults = new List<object>();
            TResult res = EvalImplementation<TResult>(query, baseCollections, 0);

            var realRecord = baseCollections.Values.FirstOrDefault(record => record.LimitReached.HasValue && record.LimitReached.Value == true);
            if (realRecord == null)
            {
                return res;
            }
           
            tempResults.Add(res);
            while (realRecord != null)
            {
                baseCollections.Clear();
                tempResults.Add(EvalImplementation<TResult>(query, baseCollections, realRecord.IndexInOriginalQueryable));
                realRecord = baseCollections.Values.FirstOrDefault(record => record.LimitReached.HasValue && record.LimitReached.Value == true);
            }

            if (combinerOfTemporaryResults == null)
            {
                combinerOfTemporaryResults = CombineTemporaryResults;
            }

            return (TResult)combinerOfTemporaryResults(tempResults);
        }

        /// <summary>
        /// Converts an expression tree that contain a function call which is not supported on a particular <see cref="IQueruable"/> provider to use an in memory implementation
        /// </summary>
        /// <param name="query">The expression tree to convert</param>
        /// <param name="combinerOfTemporaryResults">A function that is used to combine temporary results to a final one. 
        /// This function is used when the query execution has to be split because the collection to query has more elements than 
        /// the max number of results allowed in a single transaction against a persistence provider.
        /// </param>
        /// <returns>A result of a method call expression</returns>
        public object Eval(Expression expression, Func<List<object>, object> combiner = null)
        {
            var baseCollections = new Dictionary<Expression, QueryableRecord>();
            var tempResults = new List<object>();
            object res = EvalImplementation<object>(expression, baseCollections, 0);

            var realRecord = baseCollections.Values.FirstOrDefault(record => record.LimitReached.HasValue && record.LimitReached.Value == true);
            if (realRecord == null)
            {
                return res;
            }

            tempResults.Add(res);
            while (realRecord != null)
            {
                baseCollections.Clear();
                tempResults.Add(EvalImplementation<object>(expression, baseCollections, realRecord.IndexInOriginalQueryable));
                realRecord = baseCollections.Values.FirstOrDefault(record => record.LimitReached.HasValue && record.LimitReached.Value == true);
            }

            if (combiner == null)
            {
                combiner = CombineTemporaryResults;
            }

            return combiner(tempResults);
        }

        /// <summary>
        /// Combine temporary results by simply writing them to a flat list. 
        /// </summary>
        /// <param name="temporaryResults">results to combine</param>
        /// <returns>a flatten list of all temporary results</returns>
        private IQueryable CombineTemporaryResults(List<object> temporaryResults)
        {
            if (!temporaryResults.Any())
            {
                return null;
            }

            Type elementType;
            if (temporaryResults.First() is IEnumerable<object>)
            {
                elementType = (temporaryResults.First() as IEnumerable<object>).First().GetType();
            }
            else
            {
                elementType = temporaryResults.First().GetType();
            }

            var finalRes = new List<object>();   
            foreach (var item in temporaryResults)
            {
                if (item is IEnumerable<object>)
                {
                    finalRes.AddRange(item as IEnumerable<object>);
                }
                else
                {
                    finalRes.Add(item);
                }
            }

            return ExpressionHelpers.Cast(elementType, finalRes.AsQueryable());
        }


        /// <summary>
        /// Execute a query by first removing dependencies in physical repositories and then compile and execute
        /// </summary>
        /// <typeparam name="TResult">The type of the result</typeparam>
        /// <param name="query">The query to execute expressed as an expression tree</param>
        /// <param name="baseCollections">a container for the collection that the query depends on</param>
        /// <param name="skip">The number of elements to skip from the beginning</param>
        /// <returns>The result of the query</returns>
        private TResult EvalImplementation<TResult>(Expression query, Dictionary<Expression, QueryableRecord> baseCollections, int skip)
        {
            var converter = new MethodCallConverter(Provider, baseCollections, MaxCollectionSize);
            var newExp = converter.Convert(query, skip);

            LambdaExpression lambda = Expression.Lambda(newExp);
            Delegate fn = lambda.Compile();
            return (TResult)fn.DynamicInvoke(null);
        }
        
        /// <summary>
        /// Convert and execute a query only if the <see cref="IQueriable"/> expression is not supported by the query provider 
        /// </summary>
        /// <param name="query">Query to execute</param>
        /// <param name="maxResults">The max number of results allowed in a single transaction against a persistence provider</param>
        /// <param name="convertedResult">The result of the query</param>
        /// <returns>a <see cref="bool"/> that specify if the query was converted and executed</returns>
        public static bool ConvertionIsRequiredAsExpressionIfNotSupported(IQueryable query, int maxResults, out object convertedResult)
        {
            var vistor = new MethodExpressionsMarker();
            var methodsNames = vistor.Eval(query.Expression);
            var providerName = query.Provider.GetType().Name;
            List<string> knownUnsupportedFunctions;
            if (UnsupportedMethodsPerProvider.TryGetValue(providerName, out knownUnsupportedFunctions))
            {
                if (knownUnsupportedFunctions.Intersect(methodsNames).Count() > 0)
                {
                    var adapter = new QueriableProviderAdapter() { Provider = query.Provider, MaxCollectionSize = maxResults };
                    convertedResult = adapter.Eval(query.Expression);
                    return true; 
                }
            }
            
            try
            {
                var enumerator = query.GetEnumerator();
                enumerator.MoveNext();
                convertedResult = null;
                return false;
            }
            catch (NotSupportedException ex)
            {
                var unsupportedMethod = ex.Message.Split(' ').Intersect(methodsNames).First();
                UnsupportedMethodsPerProvider.AddOrUpdate(providerName,
                    (_) => new List<string>() { unsupportedMethod },
                    (_, lst) =>
                    {
                        lst.Add(unsupportedMethod);
                        return lst;
                    });


                var adapter = new QueriableProviderAdapter() { Provider = query.Provider, MaxCollectionSize = maxResults };
                convertedResult = adapter.Eval(query.Expression);
                return true;
            }
        }
    }
}