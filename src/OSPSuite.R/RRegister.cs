﻿using OSPSuite.Core;
using OSPSuite.Core.Domain;
using OSPSuite.R.MinimalImplementations;
using OSPSuite.R.Services;
using OSPSuite.Utility.Container;
using IContainer = OSPSuite.Utility.Container.IContainer;

namespace OSPSuite.R
{
   public class RRegister : Register
   {
      public override void RegisterInContainer(IContainer container)
      {
         container.AddScanner(scan =>
         {
            scan.AssemblyContainingType<RRegister>();

            //REGISTER Services
            scan.IncludeNamespaceContainingType<ISimulationRunner>();
            scan.IncludeNamespaceContainingType<DisplayUnitRetriever>();

            // This will be registered as singleton
            scan.ExcludeType<GroupRepository>();
            scan.WithConvention<OSPSuiteRegistrationConvention>();
         });

         container.Register<IGroupRepository, GroupRepository>(LifeStyle.Singleton);
      }
   }
}