﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OSPSuite.Assets;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Services;
using OSPSuite.Core.Extensions;
using OSPSuite.Utility.Exceptions;
using OSPSuite.Utility.Extensions;
using static OSPSuite.Core.Domain.Constants;
using ICoreContainerTask = OSPSuite.Core.Domain.Services.IContainerTask;

namespace OSPSuite.R.Services
{
   public interface IContainerTask
   {
      IParameter[] AllParametersMatching(IModelCoreSimulation simulation, string path);
      IContainer[] AllContainersMatching(IModelCoreSimulation simulation, string path);
      IQuantity[] AllQuantitiesMatching(IModelCoreSimulation simulation, string path);
      IMoleculeAmount[] AllMoleculesMatching(IModelCoreSimulation simulation, string path);

      /// <summary>
      ///    Returns all parameter matching <paramref name="path" /> that could meaningfully be used in a SA analysis.
      ///    For example, it will only return parameter used in model, non-categorical etc.
      /// </summary>
      IParameter[] AllParametersForSensitivityAnalysisMatching(ISimulation simulation, string path);

      IParameter[] AllParametersMatching(IContainer container, string path);
      IContainer[] AllContainersMatching(IContainer container, string path);
      IQuantity[] AllQuantitiesMatching(IContainer container, string path);
      IMoleculeAmount[] AllMoleculesMatching(IContainer container, string path);

      string[] AllQuantityPathsIn(IContainer container);
      string[] AllContainerPathsIn(IContainer container);
      string[] AllMoleculesPathsIn(IContainer container);
      string[] AllParameterPathsIn(IContainer container);

      string[] AllQuantityPathsIn(IModelCoreSimulation simulation);
      string[] AllContainerPathsIn(IModelCoreSimulation simulation);
      string[] AllMoleculesPathsIn(IModelCoreSimulation simulation);
      string[] AllParameterPathsIn(IModelCoreSimulation simulation);

   }

   public class ContainerTask : IContainerTask
   {
      private readonly IEntityPathResolver _entityPathResolver;
      private readonly ISensitivityAnalysisTask _sensitivityAnalysisTask;
      private readonly ICoreContainerTask _coreContainerTask;
      private static readonly string ALL_BUT_PATH_DELIMITER = $"[^{ObjectPath.PATH_DELIMITER}]*";
      private static readonly string PATH_DELIMITER = $"\\{ObjectPath.PATH_DELIMITER}";
      private static readonly string OPTIONAL_PATH_DELIMITER = $"(\\{PATH_DELIMITER})?";

      public ContainerTask(
         IEntityPathResolver entityPathResolver,
         ISensitivityAnalysisTask sensitivityAnalysisTask,
         ICoreContainerTask coreContainerTask)
      {
         _entityPathResolver = entityPathResolver;
         _sensitivityAnalysisTask = sensitivityAnalysisTask;
         _coreContainerTask = coreContainerTask;
      }

      public IParameter[] AllParametersMatching(IModelCoreSimulation simulation, string path) =>
         AllParametersMatching(simulation?.Model?.Root, path);

      public IContainer[] AllContainersMatching(IModelCoreSimulation simulation, string path) =>
         AllContainersMatching(simulation?.Model?.Root, path);

      public IQuantity[] AllQuantitiesMatching(IModelCoreSimulation simulation, string path) =>
         AllQuantitiesMatching(simulation?.Model?.Root, path);

      public IMoleculeAmount[] AllMoleculesMatching(IModelCoreSimulation simulation, string path) =>
         AllMoleculesMatching(simulation?.Model?.Root, path);

      public IParameter[] AllParametersForSensitivityAnalysisMatching(ISimulation simulation, string path)
      {
         var allParametersMatchingPath = AllParametersMatching(simulation, path);
         var allPotentialParametersPath = new HashSet<string>(_sensitivityAnalysisTask.PotentialVariableParameterPathsFor(simulation));
         return allParametersMatchingPath.Where(x => allPotentialParametersPath.Contains(x.ConsolidatedPath())).ToArray();
      }

      public IContainer[] AllContainersMatching(IContainer container, string path) =>
         // Distributed parameters are also containers but should not be returned from the following method
         allEntitiesMatching<IContainer>(container, path).Where(isRealContainer).ToArray();

      public IParameter[] AllParametersMatching(IContainer container, string path) =>
         allEntitiesMatching<IParameter>(container, path);

      public IQuantity[] AllQuantitiesMatching(IContainer container, string path) =>
         allEntitiesMatching<IQuantity>(container, path);

      public IMoleculeAmount[] AllMoleculesMatching(IContainer container, string path) =>
         allEntitiesMatching<IMoleculeAmount>(container, path);

      public string[] AllQuantityPathsIn(IContainer container) => allEntityPathIn<IQuantity>(container);

      public string[] AllContainerPathsIn(IContainer container) => allEntityPathIn<IContainer>(container, isRealContainer);

      public string[] AllMoleculesPathsIn(IContainer container) => allEntityPathIn<IMoleculeAmount>(container);

      public string[] AllParameterPathsIn(IContainer container) => allEntityPathIn<IParameter>(container);

      public string[] AllQuantityPathsIn(IModelCoreSimulation simulation) =>  AllQuantityPathsIn(simulation?.Model?.Root);

      public string[] AllContainerPathsIn(IModelCoreSimulation simulation) => AllContainerPathsIn(simulation?.Model?.Root);

      public string[] AllMoleculesPathsIn(IModelCoreSimulation simulation) => AllMoleculesPathsIn(simulation?.Model?.Root);

      public string[] AllParameterPathsIn(IModelCoreSimulation simulation) => AllParameterPathsIn(simulation?.Model?.Root);

      private string[] allEntityPathIn<T>(IContainer container, Func<T, bool> filterFunc = null) where T : class, IEntity
      {
         return _coreContainerTask.CacheAllChildrenSatisfying(container, filterFunc ?? (x => true)).Keys.ToArray();
      }

      private bool isRealContainer(IContainer container) => !container.IsAnImplementationOf<IDistributedParameter>() && !container.IsAnImplementationOf<IMoleculeAmount>();

      private T[] allEntitiesMatching<T>(IContainer container, string path) where T : class, IEntity
      {
         if (string.IsNullOrEmpty(path))
            return Array.Empty<T>();

         var pathArray = path.ToPathArray();
         validate(pathArray);

         // no wild cards => it's a single path and do not need to inspect 
         if (!path.Contains(WILD_CARD))
         {
            var entity = container.EntityAt<T>(pathArray);
            return entity == null ? Array.Empty<T>() : new[] {entity};
         }

         var regex = new Regex(createSearchPattern(pathArray), RegexOptions.IgnoreCase);
         var parentContainerPath = $"{_entityPathResolver.FullPathFor(container)}{ObjectPath.PATH_DELIMITER}";

         return container.GetAllChildren<T>(x => pathMatches(regex, parentContainerPath, x)).ToArray();
      }

      private void validate(string[] path)
      {
         var invalidEntries = path.Where(x => !string.Equals(x, WILD_CARD_RECURSIVE)).Where(x => x.Contains(WILD_CARD_RECURSIVE)).ToList();
         if (!invalidEntries.Any())
            return;

         var correctedEntries = invalidEntries.Select(x => x.Replace(WILD_CARD_RECURSIVE, WILD_CARD)).ToList();
         throw new OSPSuiteException(Error.WildCardRecursiveCannotBePartOfPath(WILD_CARD_RECURSIVE, invalidEntries.ToPathString(), correctedEntries.ToPathString()));
      }

      private string createSearchPattern(string[] path)
      {
         var pattern = new List<string>();
         foreach (var entry in path)
         {
            if (string.Equals(entry, WILD_CARD))
            {
               // At least one occurence of a path entry => anything except ObjectPath.PATH_DELIMITER, repeated once
               pattern.Add($"{ALL_BUT_PATH_DELIMITER}?");
               pattern.Add(PATH_DELIMITER);
            }
            else if (string.Equals(entry, WILD_CARD_RECURSIVE))
            {
               pattern.Add(".*"); //Match anything
               pattern.Add(OPTIONAL_PATH_DELIMITER);
            }
            else
            {
               pattern.Add(entry.Replace(WILD_CARD, ALL_BUT_PATH_DELIMITER));
               pattern.Add(PATH_DELIMITER);
            }
         }

         pattern.RemoveAt(pattern.Count - 1);
         var searchPattern = pattern.ToString("");
         return $"^{searchPattern}$";
      }

      private bool pathMatches(Regex regex, string parentContainerPath, IEntity entity)
      {
         //Ensure that we remove the common path part between the parent container and the entity
         var entityPath = _entityPathResolver.FullPathFor(entity).Replace(parentContainerPath, "");
         return regex.IsMatch(entityPath);
      }
   }
}