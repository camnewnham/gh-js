using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.DotNetHost;
using System;
using System.Reflection;

namespace JavascriptForGrasshopper
{
    internal static class Converter
    {
        public static JSValue JSValueFromObject(object o)
        {
            if (o == null)
            {
                return JSValue.Null;
            }
            Type type = o.GetType();
            if (type.IsPrimitive)
            {
                switch (o)
                {
                    case bool:
                        return JSValue.GetBoolean((bool)o);
                    case byte:
                    case sbyte:
                    case short:
                    case ushort:
                    case int:
                        return JSValue.CreateNumber((int)o);
                    case uint:
                    case long:
                        //case ulong:
                        return JSValue.CreateNumber((long)o);
                    case float:
                    case double:
                        return JSValue.CreateNumber((double)o);
                    case char:
                        return Convert.ToString((char)o);
                    default:
                        throw new InvalidCastException($"Unable to convert from {o.GetType()} to {nameof(JSValue)}");
                }
            }
            else if (o is string)
            {
                return (string)o;
            }
            else if (type.IsValueType)
            {
                return MarshallGeneric(o);
            }
            else if (o is object)
            {
                return MarshallGeneric(o);
            }
            else
            {
                throw new InvalidCastException($"Unable to convert from input type to {nameof(JSValue)}");
            }
        }

        private static JSValue MarshallGeneric(object obj)
        {
            MethodInfo method = typeof(JSMarshaller).GetMethod(nameof(JSMarshaller.Current.ToJS));
            MethodInfo generic = method.MakeGenericMethod(obj.GetType());
            return (JSValue)generic.Invoke(JSMarshaller.Current, new object[] { obj });
        }
    }
}
