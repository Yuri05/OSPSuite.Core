using OSPSuite.Assets;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Data;
using OSPSuite.Core.Import;
using OSPSuite.Core.Services;
using OSPSuite.Infrastructure.Import.Core;
using OSPSuite.Infrastructure.Import.Core.Mappers;
using OSPSuite.Infrastructure.Import.Services;
using OSPSuite.Presentation.Presenters.Importer;
using OSPSuite.Utility.Collections;
using System.Collections.Generic;
using System.Linq;
using OSPSuite.Presentation.Core;
using OSPSuite.Utility.Extensions;
using ImporterConfiguration = OSPSuite.Core.Import.ImporterConfiguration;


namespace OSPSuite.UI.Services
{
   public class DataImporter : IDataImporter
   {
      private readonly IDialogCreator _dialogCreator;
      private readonly IImporter _importer;
      private readonly IDataSetToDataRepositoryMapper _dataRepositoryMapper;
      private readonly IApplicationController _applicationController;


      public DataImporter(
         IDialogCreator dialogCreator,
         IImporter importer,
         IApplicationController applicationController,
         IDataSetToDataRepositoryMapper dataRepositoryMapper
      )
      {
         _dialogCreator = dialogCreator;
         _importer = importer;
         _dataRepositoryMapper = dataRepositoryMapper;
         _applicationController = applicationController;
      }

      public IList<MetaDataCategory> DefaultMetaDataCategories()
      {
         var categories = new List<MetaDataCategory>();

         var speciesCategory = createMetaDataCategory<string>(Constants.ObservedData.SPECIES, isMandatory: true, isListOfValuesFixed: true);
         categories.Add(speciesCategory);

         var organCategory = createMetaDataCategory<string>(Constants.ObservedData.ORGAN, isMandatory: true, isListOfValuesFixed: true);
         organCategory.Description = ObservedData.ObservedDataOrganDescription;
         categories.Add(organCategory);

         var compCategory = createMetaDataCategory<string>(Constants.ObservedData.COMPARTMENT, isMandatory: true, isListOfValuesFixed: true);
         compCategory.Description = ObservedData.ObservedDataCompartmentDescription;
         categories.Add(compCategory);

         var moleculeCategory = createMetaDataCategory<string>(Constants.ObservedData.MOLECULE);
         moleculeCategory.Description = ObservedData.MoleculeNameDescription;
         categories.Add(moleculeCategory);

         // Add non-mandatory metadata categories
         var molecularWeightCategory = createMetaDataCategory<double>(Constants.ObservedData.MOLECULARWEIGHT);
         molecularWeightCategory.MinValue = 0;
         molecularWeightCategory.MinValueAllowed = false;
         categories.Add(molecularWeightCategory);
         categories.Add(createMetaDataCategory<string>(Constants.ObservedData.STUDY_ID));
         categories.Add(createMetaDataCategory<string>(Constants.ObservedData.GENDER, isListOfValuesFixed: true));
         categories.Add(createMetaDataCategory<string>(Constants.ObservedData.DOSE));
         categories.Add(createMetaDataCategory<string>(Constants.ObservedData.ROUTE));
         categories.Add(createMetaDataCategory<string>(Constants.ObservedData.STUDY_ID));

         return categories;
      }

      private static MetaDataCategory createMetaDataCategory<T>(string descriptiveName, bool isMandatory = false, bool isListOfValuesFixed = false)
      {
         var category = new MetaDataCategory
         {
            Name = descriptiveName,
            DisplayName = descriptiveName,
            Description = descriptiveName,
            MetaDataType = typeof(T),
            IsMandatory = isMandatory,
            IsListOfValuesFixed = isListOfValuesFixed
         };

         return category;
      }

      public (IReadOnlyList<DataRepository> DataRepositories, ImporterConfiguration Configuration) ImportDataSets(
         IReadOnlyList<MetaDataCategory> metaDataCategories,
         IReadOnlyList<ColumnInfo> columnInfos,
         DataImporterSettings dataImporterSettings
      )
      {

         var path = _dialogCreator.AskForFileToOpen(Captions.Importer.PleaseSelectDataFile, Captions.Importer.ImportFileFilter,
            Constants.DirectoryKey.OBSERVED_DATA);

         if (string.IsNullOrEmpty(path))
            return (new List<DataRepository>(), null);

         using (var importerPresenter = _applicationController.Start<IImporterPresenter>())
         {
            importerPresenter.SetSettings(metaDataCategories, columnInfos, dataImporterSettings);
            importerPresenter.SetSourceFile(path);
            using (var importerModalPresenter = _applicationController.Start<IModalImporterPresenter>())
            {
               return importerModalPresenter.ImportDataSets(importerPresenter);
            }
         }
      }

      public IReadOnlyList<DataRepository> ImportFromConfiguration(
         ImporterConfiguration configuration,
         IReadOnlyList<MetaDataCategory> metaDataCategories,
         IReadOnlyList<ColumnInfo> columnInfos,
         DataImporterSettings dataImporterSettings
      )
      {
         if (dataImporterSettings.PromptForConfirmation)
         {
            using (var importerPresenter = _applicationController.Start<IImporterPresenter>())
            {
               importerPresenter.SetSettings(metaDataCategories, columnInfos, dataImporterSettings);
               importerPresenter.LoadConfiguration(configuration);
               using (var importerModalPresenter = _applicationController.Start<IModalImporterPresenter>())
               {
                  return importerModalPresenter.ImportDataSets(importerPresenter, configuration.Id)
                     .DataRepositories;
               }
            }
         }

         var dataSource = new DataSource(_importer);
         var dataSourceFile = _importer.LoadFile(columnInfos, configuration.FileName, metaDataCategories);
         dataSourceFile.Format.CopyParametersFromConfiguration(configuration);
         var mappings = dataSourceFile.Format.Parameters.OfType<MetaDataFormatParameter>().Select(md => new MetaDataMappingConverter()
         {
            Id = md.MetaDataId,
            Index = sheetName => md.IsColumn ? dataSourceFile.DataSheets[sheetName].RawData.GetColumnDescription(md.ColumnName).Index : -1
         }).Union
         (
            dataSourceFile.Format.Parameters.OfType<GroupByDataFormatParameter>().Select(md => new MetaDataMappingConverter()
            {
               Id = md.ColumnName,
               Index = sheetName => dataSourceFile.DataSheets[sheetName].RawData.GetColumnDescription(md.ColumnName).Index
            })
         );
         dataSource.SetMappings(dataSourceFile.Path, mappings);
         dataSource.NanSettings = configuration.NanSettings;
         dataSource.SetDataFormat(dataSourceFile.Format);
         dataSource.SetNamingConvention(configuration.NamingConventions);
         var sheets = new Cache<string, DataSheet>();
         foreach (var key in configuration.LoadedSheets)
         {
            sheets.Add(key, dataSourceFile.DataSheets[key]);
         }

         dataSource.AddSheets(sheets, columnInfos, configuration.FilterString);

         return _importer.DataSourceToDataSets(dataSource, metaDataCategories, dataImporterSettings,configuration.Id);
      }

      public ReloadDataSets CalculateReloadDataSetsFromConfiguration(IReadOnlyList<DataRepository> dataSetsToImport,
         IReadOnlyList<DataRepository> existingDataSets)
      {
         var newDataSets = dataSetsToImport.Where(dataSet => !repositoryExistsInList(existingDataSets, dataSet));
         var dataSetsToBeDeleted = existingDataSets.Where(dataSet => !repositoryExistsInList(dataSetsToImport, dataSet));
         var overwrittenDataSets = dataSetsToImport.Except(newDataSets);


         var result = new ReloadDataSets(newDataSets, overwrittenDataSets, dataSetsToBeDeleted);

         using (var reloadPresenter = _applicationController.Start<IImporterReloadPresenter>())
         {
            reloadPresenter.AddDeletedDataSets(result.DataSetsToBeDeleted.AllNames());
            reloadPresenter.AddNewDataSets(result.NewDataSets.AllNames());
            reloadPresenter.AddOverwrittenDataSets(result.OverwrittenDataSets.AllNames());
            reloadPresenter.Show();

            if (reloadPresenter.Canceled())
               return new ReloadDataSets();
         }

         return result;
      }


      private bool repositoryExistsInList(IEnumerable<DataRepository> dataRepositoryList, DataRepository targetDataRepository)
      {
         return dataRepositoryList.Any(dataRepo => targetDataRepository.ExtendedProperties.KeyValues.All(keyValuePair => dataRepo.ExtendedProperties[keyValuePair.Key].ValueAsObject.ToString() == keyValuePair.Value.ValueAsObject.ToString()));
      }
   }
}

