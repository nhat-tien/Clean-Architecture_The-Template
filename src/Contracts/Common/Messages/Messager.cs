using System.Linq.Expressions;
using Contracts.Extensions.Expressions;

namespace Contracts.Common.Messages;

public static class Messager
{
    public static Message<T> Create<T>(string? objectName = null)
        where T : class => new(objectName);

    public static Message<T> Property<T>(this Message<T> message, Expression<Func<T, object>> prop)
        where T : class
    {
        message.SetPropertyName(prop.ToStringProperty());
        return message;
    }

    public static Message<T> Property<T>(this Message<T> message, string propertyName)
        where T : class
    {
        message.SetPropertyName(propertyName);
        return message;
    }

    public static Message<T> Negative<T>(this Message<T> message, bool isNegative = true)
        where T : class
    {
        message.SetNegative(isNegative);
        return message;
    }

    public static Message<T> ObjectName<T>(this Message<T> message, string name)
        where T : class
    {
        message.SetObjectName(name);
        return message;
    }

    public static Message<T> Message<T>(
        this Message<T> message,
        string mess,
        Dictionary<string, string> translations
    )
        where T : class
    {
        message.SetCustomMessage(mess);
        message.SetCustomMessageTranslations(translations);
        return message;
    }

    public static Message<T> Message<T>(this Message<T> message, MessageType mess)
        where T : class
    {
        message.SetMessage(mess);
        return message;
    }

    public static MessageResult Build<T>(this Message<T> message)
        where T : class
    {
        return message.BuildMessage();
    }
}
