﻿using System;
using System.Linq;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.PKAnalyses;
using OSPSuite.Core.Domain.UnitSystem;
using OSPSuite.R.Domain.UnitSystem;

namespace OSPSuite.R.Services
{
   public interface IDimensionTask
   {
      IDimension DimensionByName(string dimensionName);

      IDimension DimensionForUnit(string unit);

      /// <summary>
      /// Returns the default dimension for the <paramref name="standardPKParameter"/>. Note: we use an int because there is an issue with signature matching with R
      /// </summary>
      IDimension DimensionForStandardPKParameter(int standardPKParameter);

      // We need all those overloads because rClr does not support nullable types and arrays are converted to single value when the array as only one entry!
      double[] ConvertToUnit(IDimension dimension, string targetUnit, double[] valuesInBaseUnit, double molWeight);
      double[] ConvertToUnit(IDimension dimension, string targetUnit, double[] valuesInBaseUnit);

      double[] ConvertToUnit(IDimension dimension, string targetUnit, double valueInBaseUnit, double molWeight);
      double[] ConvertToUnit(IDimension dimension, string targetUnit, double valueInBaseUnit);

      double[] ConvertToUnit(string dimensionName, string targetUnit, double[] valuesInBaseUnit, double molWeight);
      double[] ConvertToUnit(string dimensionName, string targetUnit, double[] valuesInBaseUnit);

      double[] ConvertToUnit(string dimensionName, string targetUnit, double valueInBaseUnit, double molWeight);
      double[] ConvertToUnit(string dimensionName, string targetUnit, double valueInBaseUnit);

      /// <summary>
      /// Returns an array containing all dimensions defined in the suite
      /// </summary>
      IDimension[] AllAvailableDimensions();

      /// <summary>
      /// Returns the name of all dimensions defined in the suite
      /// </summary>
      string[] AllAvailableDimensionNames();

      /// <summary>
      /// This is a dimension that will only have one unit and assume tha the user is saving the value in the expected base unit
      /// </summary>
      /// <param name="unit">Unit that will be the only unit of this dimension</param>
      IDimension CreateUserDefinedDimension(string unit);
   }

   public class DimensionTask : IDimensionTask
   {
      private readonly IDimensionFactory _dimensionFactory;

      public DimensionTask(IDimensionFactory dimensionFactory)
      {
         _dimensionFactory = dimensionFactory;
      }

      public IDimension DimensionByName(string dimensionName) => _dimensionFactory.Dimension(dimensionName);

      public IDimension DimensionForUnit(string unit) => _dimensionFactory.DimensionForUnit(unit);

      public double[] ConvertToUnit(IDimension dimension, string targetUnit, double[] valuesInBaseUnit)
      {
         return convertToUnit(dimension, targetUnit, null, valuesInBaseUnit);
      }

      public double[] ConvertToUnit(IDimension dimension, string targetUnit, double valueInBaseUnit)
      {
         return convertToUnit(dimension, targetUnit,null , valueInBaseUnit);
      }

      public double[] ConvertToUnit(string dimensionName, string targetUnit, double[] valuesInBaseUnit)
      {
         return convertToUnit(DimensionByName(dimensionName), targetUnit, null, valuesInBaseUnit);
      }

      public double[] ConvertToUnit(string dimensionName, string targetUnit, double valueInBaseUnit)
      {
         return convertToUnit(DimensionByName(dimensionName), targetUnit, null, valueInBaseUnit);
      }

      public IDimension[] AllAvailableDimensions() => _dimensionFactory.Dimensions.ToArray();

      public string[] AllAvailableDimensionNames() => _dimensionFactory.DimensionNames.OrderBy(x => x).ToArray();

      public IDimension CreateUserDefinedDimension(string unit)
      {
         return new Dimension(Constants.Dimension.NO_DIMENSION.BaseRepresentation, unit, unit);
      }

      public IDimension DimensionForStandardPKParameter(int standardPKParameterValue)
      {
         var standardPKParameter = (StandardPKParameter) standardPKParameterValue;

         switch (standardPKParameter)
         {
            case StandardPKParameter.Unknown:
               return Constants.Dimension.NO_DIMENSION;
            case StandardPKParameter.Cmax:
            case StandardPKParameter.Cmin:
            case StandardPKParameter.CTrough:
               return DimensionByName(Constants.Dimension.MOLAR_CONCENTRATION);
            case StandardPKParameter.Tmax:
            case StandardPKParameter.Tmin:
            case StandardPKParameter.Tthreshold:
            case StandardPKParameter.Mrt:
            case StandardPKParameter.Thalf:
               return DimensionByName(Constants.Dimension.TIME);
            case StandardPKParameter.AucTend:
            case StandardPKParameter.AucmTend:
            case StandardPKParameter.AucInf:
            case StandardPKParameter.AucTendInf:
               return DimensionByName(Constants.Dimension.AUC_MOLAR);
            case StandardPKParameter.FractionAucEndToInf:
               return DimensionByName(Constants.Dimension.FRACTION);
            case StandardPKParameter.Vss:
            case StandardPKParameter.Vd:
               return DimensionByName(Constants.Dimension.VOLUME_PER_BODY_WEIGHT);
            default:
               throw new ArgumentOutOfRangeException(nameof(standardPKParameter), standardPKParameter, null);
         }
      }

      public double[] ConvertToUnit(IDimension dimension, string targetUnit, double valueInBaseUnit, double molWeight)
      {
         return convertToUnit(dimension, targetUnit, molWeight, valueInBaseUnit);
      }

      public double[] ConvertToUnit(string dimensionName, string targetUnit, double[] valuesInBaseUnit, double molWeight)
      {
         return convertToUnit(DimensionByName(dimensionName), targetUnit, molWeight, valuesInBaseUnit);
      }

      public double[] ConvertToUnit(string dimensionName, string targetUnit, double valueInBaseUnit, double molWeight)
      {
         return convertToUnit(DimensionByName(dimensionName), targetUnit, molWeight, valueInBaseUnit);
      }

      public double[] ConvertToUnit(IDimension dimension, string targetUnit, double[] valuesInBaseUnit, double molWeight)
      {
         return convertToUnit(dimension, targetUnit, molWeight, valuesInBaseUnit);
      }

      private double[] convertToUnit(IDimension dimension, string targetUnit, double? molWeight, params double[] valuesInBaseUnit)
      {
         var converterContext = new DoubleArrayContext(dimension, molWeight);
         var mergedDimension = _dimensionFactory.MergedDimensionFor(converterContext);
         var unit = mergedDimension.Unit(targetUnit);
         return mergedDimension.BaseUnitValuesToUnitValues(unit, valuesInBaseUnit);
      }
   }
}