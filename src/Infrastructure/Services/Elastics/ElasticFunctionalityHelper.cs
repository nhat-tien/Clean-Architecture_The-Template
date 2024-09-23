using System.Collections;
using System.Reflection;
using Contracts.Dtos.Models;
using Contracts.Dtos.Requests;
using Contracts.Extensions;
using Contracts.Extensions.Reflections;
using Domain.Common.ElasticConfigurations;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace Infrastructure.Services.Elastics;

public static class ElasticFunctionalityHelper
{
    public static SearchRequestDescriptor<T> OrderBy<T>(
        this SearchRequestDescriptor<T> sortQuery,
        QueryRequest request
    )
        where T : class
    {
        var results = new List<SortOptions>();

        string[] sorts = request.Order?.Trim().Split(',') ?? [];
        var sortItems = GetSortItems(sorts);

        for (int i = 0; i < sortItems.Count; i++)
        {
            string property = sortItems.ElementAt(0).Key;
            string order = sortItems.ElementAt(0).Value;
            SortOrder sortOrder = order == OrderTerm.DESC ? SortOrder.Desc : SortOrder.Asc;

            bool isNestedSort = property.Contains('.');
            property += $".{ElsIndexExtension.GetKeywordName<T>(property)}";

            if (!isNestedSort)
            {
                // A.ARAW
                results.Add(SortOptions.Field(property!, new FieldSort() { Order = sortOrder }));
            }
            else
            {
                //A.B.C.CRAW
                //PATH: A
                //PATH: A.B
                List<string> nestedArray = [.. property.Trim().Split('.')];
                var nestedSort = new NestedSortValue() { Path = nestedArray[0]! };
                string name = string.Empty;
                for (int j = 0; j < nestedArray.Count - 2; j++)
                {
                    name += j == 0 ? nestedArray[i] : $".{nestedArray[i]}";
                    nestedSort = nestedSort.Nested = new NestedSortValue() { Path = name! };
                }

                var sortOptions = SortOptions.Field(
                    property!,
                    new FieldSort() { Order = sortOrder, Nested = nestedSort }
                );
                results.Add(sortOptions);
            }
        }

        return sortQuery.Sort(results);
    }

    public static QueryDescriptor<T> Search<T>(
        this QueryDescriptor<T> search,
        string? keyword,
        int deep = 1
    )
    {
        List<Query> queries = [];
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            List<KeyValuePair<PropertyType, string>> stringProperties = StringProperties(
                typeof(T),
                deep
            );
            queries.AddRange(MultiMatchQuery(stringProperties, keyword));
            queries.AddRange(PrefixQuery(stringProperties, keyword));
        }

        return search.Bool(b => b.Should(queries));
    }

    private static List<Query> WildCardQuery(
        List<KeyValuePair<PropertyType, string>> stringProperties,
        string keyword)
    {
        List<Query> queries = [];
        string keywordPattern = $"*{keyword}*";

        //*search for the same level property
        List<KeyValuePair<PropertyType, string>> properties = stringProperties.FindAll(x =>
            x.Key == PropertyType.Property
        );

        var prefixQueries = properties.Select(x =>
        {
            return new WildcardQuery(new Field(x.Value)) { Value = keywordPattern };
        });

        queries.AddRange([.. prefixQueries]);

        //* search nested properties
        //todo: [{"A" ,["A.A1","A.A2"]}, {"A.B", ["A.B.B1","A.B.B2"]}]
        List<KeyValuePair<string, string>> nestedProperties = stringProperties
            .Except(properties)
            .Select(x =>
            {
                string value = x.Value;
                int lastDot = value.LastIndexOf('.');
                return new KeyValuePair<string, string>(value[..lastDot], value);
            })
            .ToList();

        // * group and sort with the deeper and deeper of nesting
        var nestedsearch = nestedProperties
            .GroupBy(x => x.Key)
            .Select(x => new { key = x.Key, primaryProperty = x.Select(p => p.Value).ToList() })
            .OrderBy(x => x.key)
            .ToList();

        //* create nested multi_match search
        foreach (var nested in nestedsearch)
        {
            var key = nested.key;
            var parts = key.Trim().Split(".");
            NestedQuery nestedQuery = new();

            string path = string.Empty;
            for (int i = 0; i < parts.Length; i++)
            {
                if (i == 0)
                {
                    path += $"{parts[i]}";
                    nestedQuery.Path = path!;
                }
                else
                {
                    path += $".{parts[i]}";
                    NestedQuery nest = new() { Path = path! };
                    nestedQuery.Query = nest;
                    nestedQuery = nest;
                }
            }
            nestedQuery.Query = new BoolQuery()
            {
                Should =
                [
                    .. nested
                        .primaryProperty.Select(x =>
                        {
                            return new WildcardQuery(new Field(x)) { Value = keywordPattern };
                        })
                        .ToList(),
                ],
            };

            queries.Add(nestedQuery);
        }

        return queries;
    }

    private static List<Query> PrefixQuery(
        List<KeyValuePair<PropertyType, string>> stringProperties,
        string keyword
    )
    {
        List<Query> queries = [];

        //*search for the same level property
        List<KeyValuePair<PropertyType, string>> properties = stringProperties.FindAll(x =>
            x.Key == PropertyType.Property
        );

        var prefixQueries = properties.Select(x =>
        {
            return new PrefixQuery(new Field(x.Value)) { Value = $"{keyword}" };
        });

        queries.AddRange([.. prefixQueries]);

        //* search nested properties
        //todo: [{"A" ,["A.A1","A.A2"]}, {"A.B", ["A.B.B1","A.B.B2"]}]
        List<KeyValuePair<string, string>> nestedProperties = stringProperties
            .Except(properties)
            .Select(x =>
            {
                string value = x.Value;
                int lastDot = value.LastIndexOf('.');
                return new KeyValuePair<string, string>(value[..lastDot], value);
            })
            .ToList();

        // * group and sort with the deeper and deeper of nesting
        var nestedsearch = nestedProperties
            .GroupBy(x => x.Key)
            .Select(x => new { key = x.Key, primaryProperty = x.Select(p => p.Value).ToList() })
            .OrderBy(x => x.key)
            .ToList();

        //* create nested multi_match search
        foreach (var nested in nestedsearch)
        {
            var key = nested.key;
            var parts = key.Trim().Split(".");
            NestedQuery nestedQuery = new();

            string path = string.Empty;
            for (int i = 0; i < parts.Length; i++)
            {
                if (i == 0)
                {
                    path += $"{parts[i]}";
                    nestedQuery.Path = path!;
                }
                else
                {
                    path += $".{parts[i]}";
                    NestedQuery nest = new() { Path = path! };
                    nestedQuery.Query = nest;
                    nestedQuery = nest;
                }
            }
            nestedQuery.Query = new BoolQuery()
            {
                Should =
                [
                    .. nested
                        .primaryProperty.Select(x =>
                        {
                            return new PrefixQuery(new Field(x)) { Value = $"{keyword}" };
                        })
                        .ToList(),
                ],
            };

            queries.Add(nestedQuery);
        }

        return queries;
    }

    private static List<Query> MultiMatchQuery(
        List<KeyValuePair<PropertyType, string>> stringProperties,
        string keyword
    )
    {
        //*search for the same level property
        List<KeyValuePair<PropertyType, string>> properties = stringProperties.FindAll(x =>
            x.Key == PropertyType.Property
        );
        MultiMatchQuery multiMatchQuery =
            new()
            {
                Query = $"{keyword}",
                Fields = Fields.FromFields(properties.Select(x => new Field(x.Value)).ToArray()),
            };
        List<Query> queries = [multiMatchQuery];

        //* search nested properties
        //todo: [{"A" ,["A.A1","A.A2"]}, {"A.B", ["A.B.B1","A.B.B2"]}]
        List<KeyValuePair<string, string>> nestedProperties = stringProperties
            .Except(properties)
            .Select(x =>
            {
                string value = x.Value;
                int lastDot = value.LastIndexOf('.');
                return new KeyValuePair<string, string>(value[..lastDot], value);
            })
            .ToList();

        // * group and sort with the deeper and deeper of nesting
        var nestedsearch = nestedProperties
            .GroupBy(x => x.Key)
            .Select(x => new { key = x.Key, primaryProperty = x.Select(p => p.Value).ToList() })
            .OrderBy(x => x.key)
            .ToList();

        //* create nested multi_match search
        foreach (var nested in nestedsearch)
        {
            var key = nested.key;
            var parts = key.Trim().Split(".");
            NestedQuery nestedQuery = new();

            string path = string.Empty;
            for (int i = 0; i < parts.Length; i++)
            {
                if (i == 0)
                {
                    path += $"{parts[i]}";
                    nestedQuery.Path = path!;
                }
                else
                {
                    path += $".{parts[i]}";
                    NestedQuery nest = new() { Path = path! };
                    nestedQuery.Query = nest;
                    nestedQuery = nest;
                }
            }
            nestedQuery.Query = new MultiMatchQuery()
            {
                Query = $"{keyword}",
                Fields = nested.primaryProperty.Select(x => new Field(x)).ToArray(),
            };
            queries.Add(nestedQuery);
        }

        return queries;
    }

    static List<KeyValuePair<PropertyType, string>> StringProperties(
        Type type,
        int deep = 1,
        string? parrentName = null,
        PropertyType? propertyType = null
    )
    {
        parrentName = parrentName?.Trim().ToCamelCase();
        if (deep < 0)
        {
            return [];
        }

        IEnumerable<PropertyInfo> properties = type.GetProperties();

        List<KeyValuePair<PropertyType, string>> stringProperties = properties
            .Where(x => x.PropertyType == typeof(string))
            .Select(x =>
            {
                string propertyName = x.Name.ToCamelCase();
                return new KeyValuePair<PropertyType, string>(
                    propertyType ?? PropertyType.Property,
                    parrentName != null ? $"{parrentName}.{propertyName}" : propertyName
                );
            })
            .ToList();

        List<PropertyInfo> collectionObjectProperties = properties
            .Where(x =>
                (x.IsUserDefineType() || IsArrayGenericType(x)) && x.PropertyType != typeof(string)
            )
            .ToList();

        foreach (var propertyInfo in collectionObjectProperties)
        {
            string propertyName = propertyInfo.Name.ToCamelCase();
            string currentName =
                parrentName != null ? $"{parrentName}.{propertyName}" : propertyName;

            if (IsArrayGenericType(propertyInfo))
            {
                Type genericType = propertyInfo.PropertyType.GetGenericArguments()[0];
                stringProperties.AddRange(
                    StringProperties(genericType, deep - 1, currentName, PropertyType.Array)
                );
            }
            else if (propertyInfo.IsUserDefineType())
            {
                stringProperties.AddRange(
                    StringProperties(
                        propertyInfo.PropertyType,
                        deep - 1,
                        currentName,
                        PropertyType.Object
                    )
                );
            }
        }

        return stringProperties;
    }

    static List<KeyValuePair<PropertyType, string>> GivenStringProperties(
        Type type,
        IEnumerable<string> properties
    )
    {
        var result = new List<KeyValuePair<PropertyType, string>>();
        foreach (var propertyPath in properties)
        {
            var currentType = type;
            var parts = propertyPath.Split('.');
            PropertyType propertyType = PropertyType.Property;

            for (int i = 0; i < parts.Length; i++)
            {
                var propertyName = parts[i];
                var propertyInfo =
                    currentType.GetProperty(propertyName)
                    ?? throw new ArgumentException(
                        $"Property '{propertyName}' not found on type '{currentType.Name}'."
                    );

                var propertyTypeInfo = propertyInfo.PropertyType;

                if (IsArrayGenericType(propertyInfo))
                {
                    propertyType = PropertyType.Array;
                    currentType = propertyTypeInfo.GetGenericArguments()[0];
                }
                else if (
                    propertyTypeInfo.IsClass && !propertyTypeInfo.Namespace!.StartsWith("System")
                )
                {
                    propertyType = PropertyType.Object;
                    currentType = propertyTypeInfo;
                }
                else
                {
                    propertyType = PropertyType.Property;
                }

                if (i == parts.Length - 1 && propertyTypeInfo == typeof(string))
                {
                    result.Add(new KeyValuePair<PropertyType, string>(propertyType, propertyPath));
                }
            }
        }

        return result;
    }

    static bool IsArrayGenericType(PropertyInfo propertyInfo)
    {
        Type type = propertyInfo.PropertyType;

        if (
            type.IsGenericType
            && typeof(IEnumerable).IsAssignableFrom(type)
            && type.GetGenericArguments()[0].IsUserDefineType()
        )
        {
            return true;
        }
        return false;
    }

    private static Dictionary<string, string> GetSortItems(string[] sortItems)
    {
        return sortItems
            .Select(sortItem =>
            {
                string[] items = sortItem.Trim().Split(' ');

                if (items.Length == 1)
                {
                    return new KeyValuePair<string, string>(items[0].Trim(), OrderTerm.ASC);
                }

                return new KeyValuePair<string, string>(items[0].Trim(), items[1].Trim());
            })
            .ToDictionary(x => x.Key, x => x.Value);
    }

    internal enum PropertyType
    {
        Property = 1,
        Object = 2,
        Array = 3,
    }
}
