using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.DotNetHost;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace JavascriptForGrasshopper
{
    internal static class Converter
    {
        public static JSValue ToJS(object o)
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
            else if (type.IsValueType || o is object)
            {
                Node.EnsureType(type);
                return JSMarshaller.Current.ToJS(type, o);
            }
            else
            {
                throw new InvalidCastException($"Unable to convert from input type to {nameof(JSValue)}");
            }
        }

        public static object FromJS(JSValue jsVal)
        {
            if (jsVal.IsNullOrUndefined())
            {
                return null;
            }
            else if (jsVal.IsArray())
            {
                var list = new List<object>();
                foreach (var itm in jsVal.Items)
                {
                    list.Add(FromJS(itm));
                }
                return list;
            }
            else if (jsVal.IsObject())
            {
                if (jsVal.Properties["constructor"] is JSValue ctor && ctor.IsObject())
                {
                    Type type = ((JSObject)ctor).Unwrap<Type>();
                    Node.EnsureType(type);
                    return JSMarshaller.Current.FromJS(type, jsVal);
                }
                else
                {
                    // Is this a generic javascript object? How should this be returned?
                    return null;
                }
            }
            else
            {
                return jsVal.GetValueExternalOrPrimitive();
            }
        }
    }
}
