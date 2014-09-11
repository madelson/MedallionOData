using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData
{
    internal static class NumberHelper
    {
        public static bool IsNumeric(this Type @this)
        {
            switch (Type.GetTypeCode(@this))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    var underlyingType = Nullable.GetUnderlyingType(@this);
                    return @this != null && underlyingType.IsNumeric();
            }
        }

        public static bool IsFloatingPoint(this Type @this)
        {
            switch (Type.GetTypeCode(@this))
            {
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        private static volatile Dictionary<KeyValuePair<Type, Type>, object> converters = new Dictionary<KeyValuePair<Type, Type>, object>(capacity: 0);

        public static TTo CheckedConvert<TTo>(object value)
        {
            var underlyingToType = Nullable.GetUnderlyingType(typeof(TTo)) ?? typeof(TTo);
            if (value == null)
            {
                Throw.If(!underlyingToType.IsNumeric(), "TTo: must be numeric");
                if (underlyingToType == typeof(TTo))
                {
                    throw new InvalidCastException("Cannot convert null to non-nullable type " + typeof(TTo));
                }
                return default(TTo);
            }

            object converter;
            var lookupKey = KeyValuePair.Create(value.GetType(), underlyingToType);
            if (!converters.TryGetValue(lookupKey, out converter))
            {
                Throw.If(!lookupKey.Key.IsNumeric(), "value: must be numeric");
                Throw.If(!lookupKey.Value.IsNumeric(), "TTo: must be numeric");

                converter = Activator.CreateInstance(typeof(CheckedConverter<,>).MakeGenericType(lookupKey.Key, lookupKey.Value));
                lock (typeof(NumberHelper))
                {
                    var convertersCopy = new Dictionary<KeyValuePair<Type, Type>, object>(converters);
                    // assign, don't add because of the race condition (we could also do double-checked locking)
                    convertersCopy[lookupKey] = converter;
                    converters = convertersCopy;
                }
            }

            var result = lookupKey.Value == typeof(TTo)
                ? ((CheckedConverter<TTo>)converter).Convert(value)
                // when TTo is nullable, the converter won't have TTo == TTo. Thus, we call the non-generic
                // ObjectConvert method. This incurs an extra boxing, so we don't always want to do it
                : (TTo)((CheckedConverter)converter).ObjectConvert(value);
            return result;
        }

        private abstract class CheckedConverter
        {
            public abstract object ObjectConvert(object value);
        }

        private abstract class CheckedConverter<TTo> : CheckedConverter
        {
            public override object ObjectConvert(object value) 
            {
                return this.Convert(value);
            }

            public abstract TTo Convert(object value);
        }

        private class CheckedConverter<TFrom, TTo> : CheckedConverter<TTo>
        {
            private readonly Func<TFrom, TTo> converter;

            public CheckedConverter()
            {
                var parameter = Expression.Parameter(typeof(TFrom));
                var conversion = Expression.ConvertChecked(parameter, typeof(TTo));
                var lambda = Expression.Lambda<Func<TFrom, TTo>>(
                    // when converting floating -> non-floating, do an integer check
                    body: typeof(TFrom).IsFloatingPoint() && !typeof(TTo).IsFloatingPoint()
                        ? Expression.Condition(
                            test: Expression.Equal(parameter, Expression.Convert(conversion, typeof(TFrom))),
                            ifTrue: conversion,
                            ifFalse: Expression.Call(Helpers.GetMethod(() => ThrowNonIntegerValue(default(TFrom))), parameter)
                        )
                        : conversion.As<Expression>(),
                    parameters: parameter
                );
                this.converter = lambda.Compile();
            }

            private static TTo ThrowNonIntegerValue(TFrom from)
            {
                throw new InvalidCastException("Cannot convert non-integral " + typeof(TFrom) + " value " + from + " to type " + typeof(TTo));
            }

            public override TTo Convert(object value)
            {
                return this.Convert((TFrom)value);
            }

            public TTo Convert(TFrom value)
            {
                return this.converter(value);
            }
        }
    }
}
