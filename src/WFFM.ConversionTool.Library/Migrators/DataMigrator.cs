﻿using System;
using System.Collections.Generic;
using System.Linq;
using WFFM.ConversionTool.Library.Database.Forms;
using WFFM.ConversionTool.Library.Database.WFFM;
using WFFM.ConversionTool.Library.Helpers;
using WFFM.ConversionTool.Library.Models.Metadata;
using WFFM.ConversionTool.Library.Providers;
using WFFM.ConversionTool.Library.Providers.FormsData;
using WFFM.ConversionTool.Library.Repositories;
using WFFM.ConversionTool.Library.Visualization;
using FieldData = WFFM.ConversionTool.Library.Database.Forms.FieldData;

namespace WFFM.ConversionTool.Library.Migrators
{
	public class DataMigrator : IDataMigrator
	{
		private IDataProvider _dataProvider;
		private ISitecoreFormsDbRepository _sitecoreFormsDbRepository;
		private IDestMasterRepository _destMasterRepository;
		private IMetadataProvider _metadataProvider;
		private AppSettings _appSettings;

		public DataMigrator(IDataProvider dataProvider, ISitecoreFormsDbRepository sitecoreFormsDbRepository, IDestMasterRepository destMasterRepository, IMetadataProvider metadataProvider, AppSettings appSettings)
		{
			_dataProvider = dataProvider;
			_sitecoreFormsDbRepository = sitecoreFormsDbRepository;
			_destMasterRepository = destMasterRepository;
			_metadataProvider = metadataProvider;
			_appSettings = appSettings;
		}

		public void MigrateData()
		{
			Console.WriteLine("Started forms data migration...");
			Console.WriteLine();

			var convertedForms = _destMasterRepository.GetSitecoreDescendantsItems(
				_metadataProvider.GetItemMetadataByTemplateName("Form").destTemplateId,
				_appSettings.itemReferences["destFormFolderId"]).Select(form => form.ID).ToList();

			int formsCounter = 0;
			ProgressBar.DrawTextProgressBar(formsCounter, convertedForms.Count, "forms data migrated");

			foreach (Guid convertedFormId in convertedForms)
			{
				MigrateFormData(convertedFormId);
				formsCounter++;
				ProgressBar.DrawTextProgressBar(formsCounter, convertedForms.Count, "forms data migrated");
			}

			Console.WriteLine();
			Console.WriteLine("Finished forms data migration.");
			Console.WriteLine();
		}

		private void MigrateFormData(Guid formId)
		{
			var formDataRecords = _dataProvider.GetFormDataRecords(formId);

			var fieldValueTypeCollection = GetFieldValueTypeCollection(formId);

			foreach (FormData formDataRecord in formDataRecords)
			{
				var fieldDataRecords = _dataProvider.GetFieldDataRecords(formDataRecord.Id);

				List<FieldData> fieldDataFormsRecords = fieldDataRecords.Select(data => ConvertFieldData(data, fieldValueTypeCollection)).ToList();

				FormEntry formEntry = new FormEntry()
				{
					ID = formDataRecord.Id,
					FormItemID = formDataRecord.FormItemId,
					Created = formDataRecord.TimeStamp,
					FieldDatas = fieldDataFormsRecords
				};

				_sitecoreFormsDbRepository.CreateOrUpdateFormData(formEntry);
			}
		}

		private FieldData ConvertFieldData(Database.WFFM.FieldData wffmFieldData, List<FieldDataValueMetadata> collection)
		{
			return new FieldData()
			{
				FieldItemID = wffmFieldData.FieldItemId,
				FieldName = wffmFieldData.FieldName,
				FormEntryID = wffmFieldData.FormId,
				ID = wffmFieldData.Id,
				Value = GetFieldDataValue(wffmFieldData.Value, wffmFieldData.Data, collection.First(f => f.fieldId == wffmFieldData.FieldItemId)?.dataValueConverter),
				ValueType = collection.First(f => f.fieldId == wffmFieldData.FieldItemId)?.dataValueType ?? "System.String"
			};
		}

		private string GetFieldDataValue(string value, string data, string dataValueConverter)
		{
			var dataValue = !string.IsNullOrEmpty(data) ? data : value;
			dataValue = XmlHelper.StripHtml(dataValue);

			if (!string.IsNullOrEmpty(dataValueConverter))
			{
				var converter = IoC.CreateConverter(dataValueConverter);
				dataValue = converter.ConvertValue(dataValue);
			}

			return dataValue;
		}

		public class FieldDataValueMetadata
		{
			public Guid fieldId { get; set; }
			public string dataValueType { get; set; }
			public string dataValueConverter { get; set; }
		}

		private List<FieldDataValueMetadata> GetFieldValueTypeCollection(Guid formId)
		{
			var collection = new List<FieldDataValueMetadata>();

			var formDescendantsItems = _destMasterRepository.GetSitecoreDescendantsItems(formId);

			foreach (var formDescendantsItem in formDescendantsItems)
			{
				var metadataTemplate = _metadataProvider.GetItemMetadataByTemplateId(formDescendantsItem.TemplateID);
				if (metadataTemplate != null)
				{
					var dataValueType = metadataTemplate.dataValueType;
					if (!string.IsNullOrEmpty(dataValueType))
					{
						collection.Add(new FieldDataValueMetadata()
						{
							fieldId = formDescendantsItem.ID,
							dataValueType = dataValueType,
							dataValueConverter = metadataTemplate.dataValueConverter
						});
					}
				}
			}

			return collection;
		}		
	}
}
