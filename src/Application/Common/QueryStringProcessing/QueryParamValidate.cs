using System.Reflection;
using System.Text.RegularExpressions;
using Application.Common.Exceptions;
using Contracts.Common.Messages;
using Contracts.Dtos.Requests;
using Contracts.Extensions;
using Contracts.Extensions.Reflections;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Wangkanai.Extensions;

namespace Application.Common.QueryStringProcessing;

public static partial class QueryParamValidate
{
    public static QueryParamRequest ValidateQuery(this QueryParamRequest request)
    {
        if (
            !string.IsNullOrWhiteSpace(request.Cursor?.Before)
            && !string.IsNullOrWhiteSpace(request.Cursor?.After)
        )
        {
            throw new BadRequestException(
                [
                    Messager
                        .Create<QueryParamRequest>("QueryParam")
                        .Property(x => x.Cursor!)
                        .Message(MessageType.Redundant)
                        .Build(),
                ]
            );
        }

        return request;
    }

    public static QueryParamRequest ValidateFilter(this QueryParamRequest request, Type type)
    {
        if (request.OriginFilters?.Length <= 0)
        {
            return request;
        }

        List<QueryResult> queries = StringExtension
            .TransformStringQuery(request.OriginFilters!)
            .ToList();

        foreach (QueryResult query in queries)
        {
            //if it's $and,$or,$in and $between then they must have a index after
            if (!ValidateArrayOperator(query.CleanKey))
            {
                throw new BadRequestException(
                    [
                        Messager
                            .Create<QueryParamRequest>("QueryParam")
                            .Property(x => x.Filter!)
                            .Message(MessageType.ValidFormat)
                            .Negative()
                            .Build(),
                    ]
                );
            }

            // lack of operator
            if (!ValidateLackOfOperator(query.CleanKey))
            {
                throw new BadRequestException(
                    [
                        Messager
                            .Create<QueryParamRequest>("QueryParam")
                            .Property(x => x.Filter!)
                            .Message(MessageType.ValidFormat)
                            .Negative()
                            .Build(),
                    ]
                );
            }

            // if the last element is logical operator it's wrong.
            if (!LackOfElementInArrayOperator(query.CleanKey))
            {
                throw new BadRequestException(
                    [
                        Messager
                            .Create<QueryParamRequest>("QueryParam")
                            .Property(x => x.Filter!)
                            .Message(MessageType.ValidFormat)
                            .Negative()
                            .Build(),
                    ]
                );
            }

            var validKey = query.CleanKey.Where(x =>
                string.Compare(x, "$or", StringComparison.OrdinalIgnoreCase) != 0
                && string.Compare(x, "$and", StringComparison.OrdinalIgnoreCase) != 0
                && !x.IsDigit()
                && !validOperators.Contains(x.ToLower())
            );

            string key = string.Join(".", validKey);
            PropertyInfo propertyInfo = type.GetNestedPropertyInfo(key);

            //
            if (
                (propertyInfo.PropertyType.IsEnum || IsNumericType(propertyInfo.PropertyType))
                && query.Value?.IsDigit() == false
            )
            {
                throw new BadRequestException(
                    [
                        Messager
                            .Create<QueryParamRequest>("QueryParam")
                            .Property(x => x.Filter!)
                            .Message(MessageType.ValidFormat)
                            .Negative()
                            .Build(),
                    ]
                );
            }
        }

        var trimQueries = queries.Select(x => string.Join(".", x.CleanKey));

        // duplicated element of filter
        if (trimQueries.Distinct().Count() != queries.Count)
        {
            throw new BadRequestException(
                [
                    Messager
                        .Create<QueryParamRequest>("QueryParam")
                        .Property(x => x.Filter!)
                        .Message(MessageType.ValidFormat)
                        .Negative()
                        .Build(),
                ]
            );
        }

        return request;
    }

    private static bool IsNumericType(Type type)
    {
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte
            or TypeCode.SByte
            or TypeCode.UInt16
            or TypeCode.UInt32
            or TypeCode.UInt64
            or TypeCode.Int16
            or TypeCode.Int32
            or TypeCode.Int64
            or TypeCode.Decimal
            or TypeCode.Double
            or TypeCode.Single => true,
            _ => false,
        };
    }

    private static bool ValidateArrayOperator(List<string> input)
    {
        List<string> arrayOperators = ["$and", "$or", "$in", "$between"];

        return arrayOperators.Any(arrayOperator =>
        {
            int index = input.FindIndex(x => x == arrayOperator);

            if (index < 0)
            {
                return false;
            }

            string afterArrayOperator = input[index + 1];

            return afterArrayOperator.IsDigit();
        });
    }

    private static bool ValidateLackOfOperator(List<string> input)
    {
        Stack<string> inputs = new(input);

        string last = inputs.Pop();
        string preLast = inputs.Pop();

        if (arrayOperators.Contains(preLast.ToLower()))
        {
            return true;
        }

        return validOperators.Contains(last.ToLower());
    }

    private static bool LackOfElementInArrayOperator(List<string> input)
    {
        Stack<string> inputs = new(input);
        string last = inputs.Pop();
        string preLast = inputs.Pop();

        return logicalOperators.Contains(preLast.ToLower()) && last.IsDigit();
    }

    // Array of valid operators
    private static readonly string[] validOperators =
    [
        "$eq",
        "$eqi",
        "$ne",
        "$nei",
        "$in",
        "$notin",
        "$lt",
        "$lte",
        "$gt",
        "$gte",
        "$between",
        "$notcontains",
        "$notcontainsi",
        "$contains",
        "$containsi",
        "$startswith",
        "$endswith",
    ];

    // Operators that don't require further validation after them
    private static readonly string[] arrayOperators = ["$in", "$between"];

    private static readonly string[] logicalOperators = ["$and", "$or"];
}
