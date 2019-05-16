﻿using SimModelNET;

namespace OSPSuite.Engine.Domain
{
   public interface ISimModelSimulationFactory
   {
      ISimulation Create();
   }

   public class SimModelSimulationFactory : ISimModelSimulationFactory
   {
      public ISimulation Create()
      {
         return new Simulation();
      }
   }
}