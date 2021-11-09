﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Richiban.Cmdr
{
    static class EnumerableExtensions
    {
        public static IEnumerable<U> Choose<T, U>(
            this IEnumerable<T> source,
            Func<T, U?> selector)
        {
            foreach (var item in source)
            {
                var chosen = selector(item);

                if (chosen is not null)
                {
                    yield return chosen;
                }
            }
        }

        public static string
            StringJoin<T>(this IEnumerable<T> source, string separator) =>
            string.Join(separator, source);
        
        public static (ImmutableArray<TSuccess> Successes, ImmutableArray<TError> Failures) Partition<TSuccess, TError>(
            this IEnumerable<Result<TError, TSuccess>> source)
        {
            var successes = ImmutableArray.CreateBuilder<TSuccess>();
            var failures = ImmutableArray.CreateBuilder<TError>();
            
            foreach (var item in source)
            {
                switch (item)
                {
                    case Result<TError, TSuccess>.Ok(var success):
                        successes.Add(success);
                        break;
                    
                    case Result<TError, TSuccess>.Error(var failure):
                        failures.Add(failure);
                        break;
                }
            }
            

            return (successes.ToImmutable(), failures.ToImmutable());
        }
    }
}