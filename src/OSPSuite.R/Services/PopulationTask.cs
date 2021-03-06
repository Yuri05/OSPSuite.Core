﻿using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Populations;
using OSPSuite.Core.Domain.Services;
using OSPSuite.Core.Extensions;
using OSPSuite.Infrastructure.Import.Services;
using OSPSuite.R.Domain;
using OSPSuite.Utility;
using OSPSuite.Utility.Extensions;

namespace OSPSuite.R.Services
{
   public interface IPopulationTask
   {
      IndividualValuesCache ImportPopulation(string fileFullPath);

      DataTable PopulationTableFrom(IndividualValuesCache population, IModelCoreSimulation simulation = null);

      /// <summary>
      /// Loads the population from the <paramref name="populationFile"/> and split the loaded population according to the <paramref name="numberOfCores"/>.
      /// Resulting files will be exported in the <paramref name="outputFolder"/>. File names will be constructed using the <paramref name="outputFileName"/>
      /// concatenated with the core index.
      /// Returns an array of string containing the full path of the population files created
      /// </summary>
      string[] SplitPopulation(string populationFile, int numberOfCores, string outputFolder, string outputFileName);
   }

   public class PopulationTask : IPopulationTask
   {
      private readonly IIndividualValuesCacheImporter _individualValuesCacheImporter;
      private readonly IEntitiesInSimulationRetriever _entitiesInSimulationRetriever;
      private readonly RLogger _logger;

      public PopulationTask(
         IIndividualValuesCacheImporter individualValuesCacheImporter, 
         IEntitiesInSimulationRetriever entitiesInSimulationRetriever,
         RLogger logger)
      {
         _individualValuesCacheImporter = individualValuesCacheImporter;
         _entitiesInSimulationRetriever = entitiesInSimulationRetriever;
         _logger = logger;
      }

      public IndividualValuesCache ImportPopulation(string fileFullPath)
      {
         var importLogger = new ImportLogger();
         var parameterValuesCache = _individualValuesCacheImporter.ImportFrom(fileFullPath, importLogger);
         importLogger.ThrowOnError();
         _logger.Log(importLogger);
         return parameterValuesCache;
      }

      public DataTable PopulationTableFrom(IndividualValuesCache population, IModelCoreSimulation simulation = null)
      {
         var dataTable = new DataTable();
         var allParameters = _entitiesInSimulationRetriever.QuantitiesFrom(simulation);
         dataTable.BeginLoadData();

         //add individual ids column
         population.IndividualIds.Each(i => dataTable.Rows.Add(dataTable.NewRow()));
         addColumnValues(dataTable, Constants.Population.INDIVIDUAL_ID_COLUMN, population.IndividualIds);


         //Create one column for the parameter path
         addCovariates(population, dataTable);

         //add advanced parameters
         foreach (var parameterValues in population.AllParameterValues)
         {
            var parameterPathWithoutUnit = allParameters.Contains(parameterValues.ParameterPath) ? parameterValues.ParameterPath : parameterValues.ParameterPath.StripUnit();
            addColumnValues(dataTable, parameterPathWithoutUnit, parameterValues.Values);
         }

         dataTable.EndLoadData();
         return dataTable;
      }

      public string[] SplitPopulation(string populationFile, int numberOfCores, string outputFolder, string outputFileName)
      {
         var population = ImportPopulation(populationFile);
         var populationData = PopulationTableFrom(population);
         var dataSplitter = new PopulationDataSplitter(numberOfCores, populationData);
         DirectoryHelper.CreateDirectory(outputFolder);
         var outputFiles = new List<string>();

         for (int i = 0; i < numberOfCores; i++)
         {
            var outputFile = Path.Combine(outputFolder, $"{outputFileName}_{i + 1}{Constants.Filter.CSV_EXTENSION}");
            var rowIndices = dataSplitter.GetRowIndices(i).ToList();

            //This is potentially empty if the number of individuals in the population is less than the number of cores provided
            if(!rowIndices.Any())
               continue;
            
            outputFiles.Add(outputFile);
            exportSplitPopulation(populationData, rowIndices, outputFile);
         }

         return outputFiles.ToArray();
      }

      private void exportSplitPopulation(DataTable populationData, IReadOnlyList<int> rowIndices, string outputFile)
      {
         var dataTable = populationData.Clone();
         rowIndices.Each(index =>
         {
            dataTable.ImportRow(populationData.Rows[index]);
         });
         dataTable.ExportToCSV(outputFile);
      }

      private void addCovariates(IndividualValuesCache population, DataTable dataTable)
      {
         //and one column for each individual in the population
         foreach (var covariateName in population.AllCovariatesNames())
         {
            addColumnValues(dataTable, covariateName, population.AllCovariateValuesFor(covariateName));
         }
      }

      private void addColumnValues<T>(DataTable dataTable, string columnName, IReadOnlyList<T> allValues)
      {
         dataTable.AddColumn<T>(columnName);
         for (int i = 0; i < allValues.Count; i++)
         {
            dataTable.Rows[i][columnName] = allValues[i];
         }
      }
   }
}