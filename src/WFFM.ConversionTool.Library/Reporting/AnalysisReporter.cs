﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using WFFM.ConversionTool.Library.Constants;
using WFFM.ConversionTool.Library.Models.Metadata;
using WFFM.ConversionTool.Library.Models.Reporting;
using WFFM.ConversionTool.Library.Models.Sitecore;
using WFFM.ConversionTool.Library.Repositories;

namespace WFFM.ConversionTool.Library.Reporting
{
	public class AnalysisReporter : IReporter
	{
		private List<ReportingRecord> _reportingRecords = new List<ReportingRecord>();
	
		private ISourceMasterRepository _sourceMasterRepository;
		private AppSettings _appSettings;

		public string CurrentFormId { get; set; }
		public string CurrentFormName { get; set; }

		private List<string> _convertedFieldIds = new List<string>()
		{
			FormConstants.FormTitleFieldId,
			FormConstants.FormFooterFieldId,
			FormConstants.FormIntroductionFieldId,
			FormConstants.FormShowFooterFieldId,
			FormConstants.FormShowTitleFieldId,
			FormConstants.FormShowIntroductionFieldId,
			FormConstants.FormSaveToDatabaseFieldId,
			FormConstants.FormSubmitModeFieldId,
			FormConstants.FormSubmitNameFieldId,
			FormConstants.FormSuccessMessageFieldId,
			FormConstants.FormSuccessPageFieldId,
			FormConstants.FormTitleTagFieldId,
			FormConstants.FormSaveActionFieldId
		};

		public AnalysisReporter(ISourceMasterRepository sourceMasterRepository, AppSettings appSettings)
		{
			_sourceMasterRepository = sourceMasterRepository;
			_appSettings = appSettings;
		}

		public void AddUnmappedItemField(SCField field, Guid itemId)
		{
			AddReportingRecord(new ReportingRecord()
			{
				ItemId = itemId.ToString("B").ToUpper(),
				ItemName = _sourceMasterRepository.GetSitecoreItemName(itemId),
				ItemPath = _sourceMasterRepository.GetItemPath(itemId),
				ItemVersion = field.Version,
				ItemLanguage = field.Language,
				ItemTemplateId = _sourceMasterRepository.GetItemTemplateId(itemId).ToString("B").ToUpper(),
				ItemTemplateName = _sourceMasterRepository.GetSitecoreItemName(_sourceMasterRepository.GetItemTemplateId(itemId)),
				FieldId = field.FieldId.ToString("B").ToUpper(),
				FieldName = _sourceMasterRepository.GetSitecoreItemName(field.FieldId),
				FieldType = field.Type.ToString(),
				FieldValue = field.Value,
				Message = "Source Field Not Mapped"
			});
		}

		public void AddUnmappedFormFieldItem(Guid itemId, string sourceMappingFieldValue)
		{
			var isNotEmpty = !string.IsNullOrEmpty(sourceMappingFieldValue);

			AddReportingRecord(new ReportingRecord()
			{
				ItemId = itemId.ToString("B").ToUpper(),
				ItemName = _sourceMasterRepository.GetSitecoreItemName(itemId),
				ItemPath = _sourceMasterRepository.GetItemPath(itemId),
				ItemTemplateId = _sourceMasterRepository.GetItemTemplateId(itemId).ToString("B").ToUpper(),
				ItemTemplateName = _sourceMasterRepository.GetSitecoreItemName(_sourceMasterRepository.GetItemTemplateId(itemId)),
				Message = $"Form Field Item Not Mapped - Form Field Type Name = {(isNotEmpty ? _sourceMasterRepository.GetSitecoreItemName(Guid.Parse(sourceMappingFieldValue)) : sourceMappingFieldValue)}"
			});
		}

		public void AddUnmappedSaveAction(SCField field, Guid itemId, Guid saveActionId)
		{
			AddReportingRecord(new ReportingRecord()
			{
				ItemId = itemId.ToString("B").ToUpper(),
				ItemName = _sourceMasterRepository.GetSitecoreItemName(itemId),
				ItemPath = _sourceMasterRepository.GetItemPath(itemId),
				ItemVersion = field.Version,
				ItemLanguage = field.Language,
				ItemTemplateId = _sourceMasterRepository.GetItemTemplateId(itemId).ToString("B").ToUpper(),
				ItemTemplateName = _sourceMasterRepository.GetSitecoreItemName(_sourceMasterRepository.GetItemTemplateId(itemId)),
				FieldId = field.FieldId.ToString("B").ToUpper(),
				FieldName = _sourceMasterRepository.GetSitecoreItemName(field.FieldId),
				FieldType = field.Type.ToString(),
				FieldValueReferencedItemId = saveActionId.ToString("B").ToUpper(),
				FieldValueReferencedItemName = _sourceMasterRepository.GetSitecoreItemName(saveActionId),
				Message = "Form Save Action Not Mapped"
			});
		}

		public void AddUnmappedValueElementSourceField(SCField field, Guid itemId, string sourceFieldValueElementName, string sourceFieldValueElementValue)
		{
			AddReportingRecord(new ReportingRecord()
			{
				ItemId = itemId.ToString("B").ToUpper(),
				ItemName = _sourceMasterRepository.GetSitecoreItemName(itemId),
				ItemPath = _sourceMasterRepository.GetItemPath(itemId),
				ItemVersion = field.Version,
				ItemLanguage = field.Language,
				ItemTemplateId = _sourceMasterRepository.GetItemTemplateId(itemId).ToString("B").ToUpper(),
				ItemTemplateName = _sourceMasterRepository.GetSitecoreItemName(_sourceMasterRepository.GetItemTemplateId(itemId)),
				FieldId = field.FieldId.ToString("B").ToUpper(),
				FieldName = _sourceMasterRepository.GetSitecoreItemName(field.FieldId),
				FieldType = field.Type.ToString(),
				FieldValueElementName = sourceFieldValueElementName,
				FieldValueElementValue = sourceFieldValueElementValue,
				Message = "Source Field Element Value Not Mapped"
			});
		}

		private void AddReportingRecord(ReportingRecord reportingRecord)
		{
			// Set global form processing values
			reportingRecord.FormId = CurrentFormId;
			reportingRecord.FormName = CurrentFormName;

			_reportingRecords.Add(reportingRecord);
		}

		public void GenerateOutput()
		{
			// Filter out fields converted in ad-hoc converters
			_reportingRecords = _reportingRecords.Where(r => !_convertedFieldIds.Contains(r.FieldId) 
			                                                 || (string.Equals(r.FieldId, FormConstants.FormSaveActionFieldId, StringComparison.InvariantCultureIgnoreCase) 
			                                                     && !string.IsNullOrEmpty(r.FieldValueReferencedItemId)))
				.OrderBy(record => record.ItemPath).ToList();

			// Filter out base standard fields if analysis_ExcludeBaseStandardFields is set to true
			if (_appSettings.analysis_ExcludeBaseStandardFields)
			{
				_reportingRecords = _reportingRecords.Where(r => string.IsNullOrEmpty(r.FieldName) || !r.FieldName.StartsWith("__")).ToList();
			}

			// Convert to CSV file
			var filePath = $"Analysis\\AnalysisReport.{DateTime.Now.ToString("yyyyMMdd.hhmmss")}.csv";
			using (var writer = new StreamWriter(filePath))
			using (var csv = new CsvWriter(writer))
			{
				csv.WriteRecords(_reportingRecords);
			}

			Console.WriteLine();
			Console.WriteLine("  Conversion analysis report has been generated and saved here: " + filePath);
			Console.WriteLine();
		}
	}
}
