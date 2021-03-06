﻿
using Microsoft.Extensions.Logging;
using Serilog;

namespace OSPSuite.Infrastructure.Services
{
   public static class LoggingBuilderExtensions
   {
      public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string logFileFullPath)
      {
         builder.AddSerilog(
           new LoggerConfiguration()
             .WriteTo.File(logFileFullPath)
             .CreateLogger()
         );
         return builder;
      }
   }
}
