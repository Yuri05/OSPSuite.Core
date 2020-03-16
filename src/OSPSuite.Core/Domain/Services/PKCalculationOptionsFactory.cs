﻿using System.Collections.Generic;
using System.Linq;
using OSPSuite.Core.Domain.Builder;
using OSPSuite.Core.Extensions;

namespace OSPSuite.Core.Domain.Services
{
   public interface IPKCalculationOptionsFactory
   {
      PKCalculationOptions CreateFor(IModelCoreSimulation simulation, string moleculeName);
      void UpdateTotalDrugMassPerBodyWeight(IModelCoreSimulation simulation, string moleculeName, PKCalculationOptions options, IReadOnlyList<PKCalculationOptionsFactory.ApplicationParameters> allApplicationParametersOrderedByStartTime);
      IReadOnlyList<PKCalculationOptionsFactory.ApplicationParameters> AllApplicationParametersOrderedByStartTimeFor(IModelCoreSimulation simulation, string moleculeName);
   }

   public class PKCalculationOptionsFactory : IPKCalculationOptionsFactory
   {
      public PKCalculationOptions CreateFor(IModelCoreSimulation simulation, string moleculeName)
      {
         var options = new PKCalculationOptions();
         var endTime = (simulation.EndTime ?? 0).ToFloat();

         var allApplicationParameters = AllApplicationParametersOrderedByStartTimeFor(simulation, moleculeName);

         // all application start times starting before the end of the simulation
         var applicationStartTimes = allApplicationParameters.Select(x => x.StartTime.Value).ToFloatArray();
         var applicationEndTimes = new List<float>(applicationStartTimes.Skip(1)) {endTime};

         for (int i = 0; i < applicationStartTimes.Length; i++)
         {
            var dosingInterval = new DosingInterval
            {
               StartValue = applicationStartTimes[i],
               EndValue = applicationEndTimes[i]
            };
            options.AddInterval(dosingInterval);
         }

         // single dosing
         if (applicationStartTimes.Length <= 1)
         {
            options.InfusionTime = allApplicationParameters.FirstOrDefault()?.InfusionTime?.Value;
         }

         // Once all dosing are defined, update total drug mass
         UpdateTotalDrugMassPerBodyWeight(simulation, moleculeName, options, allApplicationParameters);

         return options;
      }

      public virtual void UpdateTotalDrugMassPerBodyWeight(IModelCoreSimulation simulation, string moleculeName, PKCalculationOptions options, IReadOnlyList<ApplicationParameters> allApplicationParametersOrderedByStartTime)
      {
         options.TotalDrugMassPerBodyWeight = simulation.TotalDrugMassPerBodyWeightFor(moleculeName);
      }

      private IReadOnlyList<IContainer> allApplicationsForMolecule(IModelCoreSimulation simulation, string moleculeName)
      {
         var applicationEventGroup = simulation.Model.Root.GetChildren<IEventGroup>().ToList();
         if (!applicationEventGroup.Any())
            return new List<IContainer>();

         var allApplications = applicationEventGroup.SelectMany(x => x.GetAllChildren<IContainer>(c => c.ContainerType == ContainerType.Application)).ToList();
         return getApplicationsForAppliedAncestorMolecule(simulation.Reactions, moleculeName, allApplications);
      }

      public IReadOnlyList<ApplicationParameters> AllApplicationParametersOrderedByStartTimeFor(IModelCoreSimulation simulation, string moleculeName)
      {
         var endTime = simulation.EndTime ?? 0;
         var allApplications = allApplicationsForMolecule(simulation, moleculeName).ToList();

         return allApplicationParametersIn(allApplications, Constants.Parameters.START_TIME)
            .Where(x => x.Value <= endTime)
            .OrderBy(x => x.Value)
            .Select(x => new ApplicationParameters(x)).ToList();
      }

      private IReadOnlyList<IContainer> getApplicationsForAppliedAncestorMolecule(IEnumerable<IReactionBuilder> reactions, string moleculeName, IReadOnlyList<IContainer> allApplications)
      {
         if (reactions == null)
            return new List<IContainer>();

         var reactionsList = reactions.ToList();
         var applicationsForAppliedAncestorMolecule = allApplications.Where(c => c.GetSingleChildByName<IMoleculeAmount>(moleculeName) != null).ToList();
         // If there are any applications of this molecule, use them
         if (applicationsForAppliedAncestorMolecule.Any())
            return applicationsForAppliedAncestorMolecule;

         // If more than one reaction produces the molecule, count the unique educts. If there is more than one, then these reactions aren't considered
         var producingMolecule = reactionsProducingMolecule(reactionsList, moleculeName);
         var distinctEductsFromReactions = eductNamesFromReactions(producingMolecule);

         if (distinctEductsFromReactions.Count() != 1)
            return new List<IContainer>();

         // Otherwise find 'parent applications' by looking recursively at reactions involving the named molecule where there is one educt and one product.
         return getApplicationsForAppliedAncestorMolecule(reactionsList, distinctEductsFromReactions[0], allApplications).ToList();
      }

      private IReadOnlyList<string> eductNamesFromReactions(IEnumerable<IReactionBuilder> reactions)
      {
         return reactions.Where(reaction => reaction.Educts.Count() == 1).SelectMany(reaction => reaction.Educts.Select(educt => educt.MoleculeName)).Distinct().ToList();
      }

      private IReadOnlyList<IReactionBuilder> reactionsProducingMolecule(IEnumerable<IReactionBuilder> reactions, string moleculeName)
      {
         return reactions.Where(reaction => reaction.Products.Count() == 1 && string.Equals(reaction.Products.First().MoleculeName, moleculeName)).ToList();
      }

      private IEnumerable<IParameter> allApplicationParametersIn(IEnumerable<IContainer> allApplicationContainers, string parameterName)
      {
         return allApplicationContainers.SelectMany(x => x.GetAllChildren<IParameter>())
            .Where(x => x.IsNamed(parameterName));
      }

      private double? infusionTimeFor(IEnumerable<IContainer> allApplicationContainers)
      {
         var infusionTimes = allOrderedApplicationParameterValues(allApplicationContainers, Constants.Parameters.INFUSION_TIME).ToList();
         if (infusionTimes.Any())
            return infusionTimes.First();

         return null;
      }

      private IEnumerable<double> allOrderedApplicationParameterValues(IEnumerable<IContainer> allApplicationContainers, string parameterName)
      {
         return allApplicationParametersIn(allApplicationContainers, parameterName)
            .Select(x => x.Value).OrderBy(t => t);
      }

      public class ApplicationParameters
      {
         public IParameter StartTime { get;  }
         public IParameter InfusionTime { get;  }
         public IParameter DrugMass { get;  }

         public ApplicationParameters(IParameter startTime)
         {
            StartTime = startTime;
            InfusionTime = startTime.ParentContainer?.Parameter(Constants.Parameters.INFUSION_TIME);
            DrugMass = startTime.ParentContainer?.Parameter(Constants.Parameters.DRUG_MASS);
         }

         public ApplicationParameters(IParameter startTime, IParameter drugMass, IParameter infusionTime)
         {
            StartTime = startTime;
            InfusionTime = infusionTime;
            DrugMass = drugMass;
         }
      }
   }
}