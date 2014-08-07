﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.OData.Core.UriParser.Semantic;

namespace System.Web.OData.OData.Query.Aggregation
{
    /// <summary>
    /// Base type for implementation class of aggregation methods
    /// </summary>
    public abstract class AggregationImplementationBase : ApplyImplementationBase
    {
        /// <summary>
        /// Execute the aggregation method
        /// </summary>
        /// <param name="elementType">The element type on which the aggregation method operates</param>
        /// <param name="query">The collection on which to execute</param>
        /// <param name="transformation">The name of the aggregation transformation</param>
        /// <returns>The result of the aggregation</returns>
        public abstract object DoAggregatinon(Type elementType, IQueryable query, ApplyAggregateClause transformation, LambdaExpression propertyToAggregateExpression);

        /// <summary>
        /// Determines the type that is returned from the aggregation method 
        /// </summary>
        /// <param name="elementType">The element type on which the aggregation method operates</param>
        /// <param name="transformation">The name of the aggregation transformation</param>
        /// <returns>The type that is returned from the aggregation method</returns>
        public abstract Type GetResultType(Type elementType, ApplyAggregateClause transformation);

        /// <summary>
        /// Helper method to get the type of a property path such as Sales.Product.Category.Name. 
        /// This method returns the type of the Name property.
        /// </summary>
        /// <param name="entityType">The type to investigate</param>
        /// <param name="propertyPath">The property path</param>
        /// <returns></returns>
        protected Type GetAggregatedPropertyType(Type entityType, string propertyPath)
        {
            Contract.Assert(!string.IsNullOrEmpty(propertyPath));
            Contract.Assert(entityType != null);

            var propertyInfo = GetPropertyInfo(entityType, propertyPath);
            if (propertyInfo != null && propertyInfo.Count > 0)
            {
                return propertyInfo.Last().PropertyType;
            }

            return null;
        }

        /// <summary>
        /// Create a projection lambda if one was not provided
        /// </summary>
        /// <param name="elementType">The type of entities</param>
        /// <param name="transformation">The transformation clause created by the parser</param>
        /// <param name="propertyToAggregateExpression">Projection Expression to that defines access to the property to aggregate</param>
        /// <returns></returns>
        protected static LambdaExpression GetProjectionLambda(Type elementType, ApplyAggregateClause transformation,
            LambdaExpression propertyToAggregateExpression)
        {
            LambdaExpression projectionLambda;
            if (propertyToAggregateExpression == null)
            {
                projectionLambda = GetProjectionLambda(elementType, transformation.AggregatableProperty);
            }
            else
            {
                projectionLambda = propertyToAggregateExpression as LambdaExpression;
            }
            return projectionLambda;
        }


        /// <summary>
        /// Create an expression that validate that a path to a property does not contain null values
        /// For example: for the query product/category/name, create expression such as x.product != null && x.product.Category != null
        /// </summary>
        /// <param name="elementType"></param>
        /// <param name="transformation"></param>
        /// <returns></returns>
        protected IQueryable FilterNullValues(IQueryable query, Type elementType, ApplyAggregateClause transformation)
        {
            var entityParam = Expression.Parameter(elementType, "e");
            IQueryable queryToUse = query;

            var accessorExpressions = GetProjectionExpressions(transformation.AggregatableProperty, entityParam).ToArray();
            var accessorExpressionsToUse = new List<Expression>();
            for (int i = 0; i < accessorExpressions.Length - 1; i++)
            {
                accessorExpressionsToUse.Add(
                    Expression.Lambda(Expression.MakeBinary(ExpressionType.NotEqual, accessorExpressions[i], Expression.Constant(null)), 
                    entityParam));
            }

            foreach (var exp in accessorExpressionsToUse)
            {
                queryToUse = ExpressionHelpers.Where(queryToUse, exp, elementType);
            }
            return queryToUse;
        }
    }
}
