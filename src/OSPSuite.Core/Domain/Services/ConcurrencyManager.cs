﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OSPSuite.Core.Domain.Services
{
   public interface IConcurrencyManager
   {
      /// <summary>
      /// </summary>
      /// <typeparam name="TData">Data type to consume by the worker function</typeparam>
      /// <typeparam name="TResult">Data produced by the worker function</typeparam>
      /// <param name="numberOfCoresToUse">Number of cores to use. Use 0 or negative to take all cores</param>
      /// <param name="cancellationToken">Cancellation token to cancel the threads</param>
      /// <param name="data">List of data to consume by the workers</param>
      /// <param name="action">A function to run on each worker on each piece of data</param>
      /// <returns>Dictionary binding a result for each input data after running the action on it</returns>
      Task<IReadOnlyDictionary<TData, TResult>> RunAsync<TData, TResult>(int numberOfCoresToUse, CancellationToken cancellationToken, IReadOnlyList<TData> data, Func<int, CancellationToken, TData, Task<TResult>> action);
   }

   public class ConcurrencyManager : IConcurrencyManager
   {
      private readonly int _maximumNumberOfCoresToUse = Math.Max(1, Environment.ProcessorCount - 1);
      public async Task<IReadOnlyDictionary<TData, TResult>> RunAsync<TData, TResult>(int numberOfCoresToUse, CancellationToken cancellationToken, IReadOnlyList<TData> data, Func<int, CancellationToken, TData, Task<TResult>> action)
      {
         if (numberOfCoresToUse <= 0)
            numberOfCoresToUse = _maximumNumberOfCoresToUse;
         var concurrentData = new ConcurrentQueue<TData>(data);
         numberOfCoresToUse = Math.Min(numberOfCoresToUse, concurrentData.Count);

         var results = new ConcurrentDictionary<TData, TResult>();
         //Starts one task per core
         var tasks = Enumerable.Range(0, numberOfCoresToUse).Select(async coreIndex =>
         {
            //While there is data left
            while (concurrentData.TryDequeue(out var datum))
            {
               cancellationToken.ThrowIfCancellationRequested();

               //Invoke the action on it and store the result
               var result = await action.Invoke(coreIndex, cancellationToken, datum);
               results.TryAdd(datum, result);
            }
         }).ToList();

         await Task.WhenAll(tasks);
         //all tasks are completed. Can return results

         var tt = results.Values;
         return results;
      }
   }
}