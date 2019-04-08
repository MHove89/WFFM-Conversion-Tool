﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using WFFM.ConversionTool.Library.Models.Metadata;

namespace WFFM.ConversionTool.Library.Providers
{
	public class MetadataProvider : IMetadataProvider
	{
		private AppSettings _appSettings;
		private List<MetadataTemplate> _metadataTemplates = new List<MetadataTemplate>();
		private string[] _metadataFiles;

		public MetadataProvider(AppSettings appSettings)
		{
			_appSettings = appSettings;

			_metadataFiles = GetMetadataFileList();

			foreach (string filePath in _metadataFiles)
			{
				_metadataTemplates.Add(GetItemMetadataByFilePath(filePath));
			}
		}

		public MetadataTemplate GetItemMetadataByTemplateId(Guid templateId)
		{
			return _metadataTemplates.FirstOrDefault(m => m.sourceTemplateId == templateId || m.destTemplateId == templateId);
		}

		public MetadataTemplate GetItemMetadataByTemplateName(string templateName)
		{
			return _metadataTemplates.FirstOrDefault(m => string.Equals(m.sourceTemplateName, templateName, StringComparison.InvariantCultureIgnoreCase) 
			                                              || string.Equals(m.destTemplateName, templateName, StringComparison.InvariantCultureIgnoreCase));
		}

		public MetadataTemplate GetItemMetadataBySourceMappingFieldValue(string mappingValue)
		{
			return _metadataTemplates.FirstOrDefault(m => string.Equals(m.sourceMappingFieldValue, mappingValue, StringComparison.InvariantCultureIgnoreCase));
		}
		
		private string[] GetMetadataFileList()
		{
			var metadataDirPath = _appSettings.metadataFolderRelativePath;
			return Directory.GetFiles(metadataDirPath, "*.json", SearchOption.AllDirectories);
		}

		private MetadataTemplate GetItemMetadataByFilePath(string filePath)
		{
			// Read json file
			var itemMeta = System.IO.File.ReadAllText(filePath);
			// Deserialize Json to Object
			MetadataTemplate metadataTemplate = JsonConvert.DeserializeObject<MetadataTemplate>(itemMeta);

			if (string.IsNullOrEmpty(metadataTemplate.baseTemplateMetadataFileName)) return metadataTemplate;
			var baseTemplateMetadataFilePath = _metadataFiles.FirstOrDefault(f => GetFileName(f).Equals(metadataTemplate.baseTemplateMetadataFileName, StringComparison.InvariantCultureIgnoreCase));
			var fullMetadataTemplate = MergeBaseMetadataTemplate(metadataTemplate, baseTemplateMetadataFilePath);

			return fullMetadataTemplate;
		}

		private string GetFileName(string filePath)
		{
			var filename = filePath.Split('\\').Last();
			return filename;
		}

		private MetadataTemplate MergeBaseMetadataTemplate(MetadataTemplate metadataTemplate, string baseTemplateMetadataFilePath)
		{
			if (string.IsNullOrEmpty(baseTemplateMetadataFilePath)) return metadataTemplate;

			MetadataTemplate baseTemplateMeta = GetItemMetadataByFilePath(baseTemplateMetadataFilePath);

			if (baseTemplateMeta == null) return metadataTemplate;

			if (baseTemplateMeta.baseTemplateMetadataFileName != null)
			{
				var filePath = _metadataFiles.FirstOrDefault(f => f.IndexOf(baseTemplateMeta.baseTemplateMetadataFileName, StringComparison.InvariantCultureIgnoreCase) > -1);
				baseTemplateMeta = MergeBaseMetadataTemplate(baseTemplateMeta, filePath);
			}

			// Add base fields
			if (baseTemplateMeta.fields.newFields != null)
			{
				if (metadataTemplate.fields.newFields != null)
				{
					foreach (var newField in baseTemplateMeta.fields.newFields)
					{
						if (metadataTemplate.fields.newFields.All(f => f.destFieldId != newField.destFieldId))
						{
							metadataTemplate.fields.newFields.Add(newField);
						}
					}
				}
				else
				{
					metadataTemplate.fields.newFields = baseTemplateMeta.fields.newFields;
				}
			}
			if (baseTemplateMeta.fields.convertedFields != null)
			{
				if (metadataTemplate.fields.convertedFields != null)
				{
					foreach (var convertedField in baseTemplateMeta.fields.convertedFields)
					{
						// Check if metadataTemplate contains it already
						var metaConvertedField = metadataTemplate.fields.convertedFields.FirstOrDefault(cf => cf.sourceFieldId == convertedField.sourceFieldId);
						if (metaConvertedField != null)
						{
							if (metaConvertedField.destFields != null && metaConvertedField.destFields.Any())
							{
								foreach (var convertedFieldDestField in convertedField.destFields)
								{
									if (metaConvertedField.destFields.All(df => !string.Equals(df.sourceElementName, convertedFieldDestField.sourceElementName, StringComparison.InvariantCultureIgnoreCase)))
									{
										metaConvertedField.destFields.Add(convertedFieldDestField);
									}
								}
							}
							else if (metaConvertedField.destFieldId == null)
							{
								metaConvertedField.destFields = convertedField.destFields;
							}
						}
						else
						{
							metadataTemplate.fields.convertedFields.Add(convertedField);
						}
					}
				}
				else
				{
					metadataTemplate.fields.convertedFields = baseTemplateMeta.fields.convertedFields;
				}
			}
			if (baseTemplateMeta.fields.existingFields != null)
			{
				if (metadataTemplate.fields.existingFields != null)
				{
					foreach (var newField in baseTemplateMeta.fields.existingFields)
					{
						if (metadataTemplate.fields.existingFields.All(f => f.fieldId != newField.fieldId))
						{
							metadataTemplate.fields.existingFields.Add(newField);
						}
					}
				}
				else
				{
					metadataTemplate.fields.existingFields = baseTemplateMeta.fields.existingFields;
				}
			}

			metadataTemplate.fields.convertedFields = metadataTemplate.fields.convertedFields != null ?  metadataTemplate.fields.convertedFields.Distinct().ToList() : metadataTemplate.fields.convertedFields;
			metadataTemplate.fields.existingFields = metadataTemplate.fields.existingFields != null ? metadataTemplate.fields.existingFields.Distinct().ToList() : metadataTemplate.fields.existingFields;
			metadataTemplate.fields.newFields = metadataTemplate.fields.newFields != null ? metadataTemplate.fields.newFields.Distinct().ToList() : metadataTemplate.fields.newFields;

			return metadataTemplate;
		}
	}
}
