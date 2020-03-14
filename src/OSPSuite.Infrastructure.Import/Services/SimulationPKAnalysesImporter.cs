﻿using System;
using System.Collections.Generic;
using System.Linq;
using LumenWorks.Framework.IO.Csv;
using OSPSuite.Assets;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.UnitSystem;
using OSPSuite.Core.Extensions;
using OSPSuite.Core.Services;
using OSPSuite.Infrastructure.Import.Extensions;
using OSPSuite.Utility.Collections;
using OSPSuite.Utility.Exceptions;
using OSPSuite.Utility.Extensions;

namespace OSPSuite.Infrastructure.Import.Services
{
   public interface ISimulationPKAnalysesImporter
   {
      IEnumerable<QuantityPKParameter> ImportPKParameters(string fileFullPath, IImportLogger logger);
   }

   public class SimulationPKAnalysesImporter : ISimulationPKAnalysesImporter
   {
      private readonly IDimensionFactory _dimensionFactory;
      private Cache<string, QuantityPKParameter> _importedPK;
      private Cache<QuantityPKParameter, List<Tuple<int, float>>> _valuesCache;
      private const int INDIVIDUAL_ID = 0;
      private const int QUANTITY_PATH = 1;
      private const int PARAMETER_NAME = 2;
      private const int VALUE = 3;
      private const int UNIT = 4;
      private const int NUMBER_OF_COLUMNS = UNIT;

      public SimulationPKAnalysesImporter(IDimensionFactory dimensionFactory)
      {
         _dimensionFactory = dimensionFactory;
      }

      public IEnumerable<QuantityPKParameter> ImportPKParameters(string fileFullPath, IImportLogger logger)
      {
         try
         {
            _importedPK = new Cache<string, QuantityPKParameter>(x => x.Id);
            //cache containing a list of tupe<individual Id, value in core unit>
            _valuesCache = new Cache<QuantityPKParameter, List<Tuple<int, float>>>();
            using (var reader = new CsvReaderDisposer(fileFullPath))
            {
               var csv = reader.Csv;
               var headers = csv.GetFieldHeaders();
               validateFileFormat(headers);
               while (csv.ReadNextRecord())
               {
                  var pkParameter = retrieveOrCreatePKParameterFor(csv);
                  addValues(pkParameter, csv);
               }
            }

            foreach (var keyValue in _valuesCache.KeyValues)
            {
               var pkParameter = keyValue.Key;
               var values = keyValue.Value;
               //0-based id
               var maxIndividualId = values.Select(x => x.Item1).Max();
               pkParameter.SetNumberOfIndividuals(maxIndividualId + 1);

               foreach (var value in values)
               {
                  pkParameter.SetValue(value.Item1, value.Item2);
               }
            }

            return _valuesCache.Keys.ToList();
         }
         catch (Exception e)
         {
            logger.AddError(e.FullMessage());
            return Enumerable.Empty<QuantityPKParameter>();
         }
         finally
         {
            _importedPK.Clear();
            _valuesCache.Clear();
         }
      }

      private void addValues(QuantityPKParameter pkParameter, CsvReader csv)
      {
         if (!_valuesCache.Contains(pkParameter))
            _valuesCache.Add(pkParameter, new List<Tuple<int, float>>());

         var coreUnit = convertValueToCoreValue(pkParameter.Dimension, csv.DoubleAt(VALUE), csv[UNIT]);
         _valuesCache[pkParameter].Add(new Tuple<int, float>(csv.IntAt(INDIVIDUAL_ID), coreUnit.ToFloat()));
      }

      private double convertValueToCoreValue(IDimension dimension, double valueInUnit, string unitName)
      {
         var unit = dimension.Unit(unitName);
         if (unit == null)
            throw new OSPSuiteException(Error.UnitIsNotDefinedInDimension(dimension.Name, unitName));

         return dimension.UnitValueToBaseUnitValue(unit, valueInUnit);
      }

      private QuantityPKParameter retrieveOrCreatePKParameterFor(CsvReader csv)
      {
         var parameterName = csv[PARAMETER_NAME];
         var quantityPath = csv[QUANTITY_PATH];
         var id = QuantityPKParameter.CreateId(quantityPath, parameterName);
         if (!_importedPK.Contains(id))
         {
            var dimension = findDimensionFor(csv[UNIT]);
            var pkParameter = new QuantityPKParameter {Name = parameterName, QuantityPath = quantityPath, Dimension = dimension};
            _importedPK.Add(pkParameter);
         }

         return _importedPK[id];
      }

      private IDimension findDimensionFor(string unit)
      {
         if (string.IsNullOrEmpty(unit))
            return _dimensionFactory.NoDimension;


         var dimension = _dimensionFactory.DimensionForUnit(unit);

         if (dimension != null)
            return dimension;


         throw new OSPSuiteException(Error.CouldNotFindDimensionWithUnit(unit));
      }

      private void validateFileFormat(string[] headers)
      {
         var exception = new OSPSuiteException(Error.SimulationPKAnalysesFileDoesNotHaveTheExpectedFormat);
         if (headers.Length <= NUMBER_OF_COLUMNS)
            throw exception;

         //check if headers are actually real strings
         if (double.TryParse(headers[0], out _))
            throw exception;
      }
   }
}