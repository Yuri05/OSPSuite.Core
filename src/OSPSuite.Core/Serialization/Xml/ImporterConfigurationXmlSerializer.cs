﻿using OSPSuite.Core.Import;

namespace OSPSuite.Core.Serialization.Xml
{
   public class ImporterConfigurationXmlSerializer : OSPSuiteXmlSerializer<ImporterConfiguration>
   {
      public override void PerformMapping()
      {
         MapEnumerable(x => x.Parameters, x => x.AddParameter);
         Map(x => x.FileName);
         Map(x => x.NamingConventions);
         MapEnumerable(x => x.LoadedSheets, x => x.LoadedSheets.Add);
         Map(x => x.FilterString);
         Map(x => x.NanSettings);
         Map(x => x.Id);
      }
   }
}
