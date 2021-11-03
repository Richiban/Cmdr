﻿using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Richiban.Cmdr;

namespace Tracer
{
    internal class DefaultLoggerAdapter : LoggerAdapterBase
    {
        private readonly Type _type;

        public DefaultLoggerAdapter(Type type)
        {
            _type = type;
        }

        public override void TraceEnter(
            string methodInfo,
            [AllowNull] string[] paramNames,
            [AllowNull] object[] paramValues)
        {
            if (methodInfo.Contains(".ctor"))
            {
                CommandLine.Trace($"[Trace]: Creating {_type} with");
            }
            else
            {
                CommandLine.Trace(
                    $"[Trace]: Entering {_type}.{GetMethodName(methodInfo)} with");
            }

            TraceArguments(paramNames, paramValues);
        }

        public override void TraceLeave(
            string methodInfo,
            long startTicks,
            long endTicks,
            [AllowNull] string[] paramNames,
            [AllowNull] object[] paramValues)
        {
            if (_type == typeof(CommandLine))
            {
                CommandLine.Trace(
                    $"[Trace]: Execution completed in {TimeSpan.FromTicks(endTicks - startTicks)}");
            }

            if (paramValues == null || paramValues.Length < 1 || paramValues[0] == null)
            {
                return;
            }

            CommandLine.Trace($"[Trace]: {GetMethodName(methodInfo)} returned");

            TraceArguments(paramNames, paramValues);
        }

        private static void TraceArguments(string[] paramNames, object[] paramValues)
        {
            if (paramNames != null && paramValues != null)
            {
                foreach (var i in Enumerable.Range(
                    start: 0,
                    Math.Min(paramNames.Length, paramValues.Length)))
                {
                    TraceArgument(paramNames[i], paramValues[i]);
                }
            }
        }

        private static void TraceArgument(string paramName, object paramValue)
        {
            if (paramValue == null)
            {
                return;
            }

            if (!(paramValue is string) && paramValue is IEnumerable e)
            {
                CommandLine.Trace($"{paramName} => ", indentationLevel: 1);

                foreach (var item in e)
                {
                    CommandLine.Trace($"{item}", indentationLevel: 2);
                }

                return;
            }

            CommandLine.Trace($"{paramName} => {paramValue}", indentationLevel: 1);
        }
    }
}