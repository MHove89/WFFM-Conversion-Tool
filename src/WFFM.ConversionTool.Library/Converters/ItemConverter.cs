﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using WFFM.ConversionTool.Library.Factories;
using WFFM.ConversionTool.Library.Models;
using WFFM.ConversionTool.Library.Models.Metadata;
using WFFM.ConversionTool.Library.Models.Sitecore;
using WFFM.ConversionTool.Library.Readers;

namespace WFFM.ConversionTool.Library.Converters
{
	public class ItemConverter : IItemConverter
	{
		private IFieldFactory _fieldFactory;
		private MetadataTemplate _itemMetadataTemplate;
		private AppSettings _appSettings;
		private IMetadataReader _metadataReader;

		public ItemConverter(IFieldFactory fieldFactory, AppSettings appSettings, IMetadataReader metadataReader)
		{
			_fieldFactory = fieldFactory;
			_appSettings = appSettings;
			_metadataReader = metadataReader;
		}

		public SCItem Convert(SCItem scItem, Guid destParentId)
		{
			_itemMetadataTemplate = _metadataReader.GetItemMetadata(scItem.TemplateID);
			return ConvertItemAndFields(scItem, destParentId);
		}

		private SCItem ConvertItemAndFields(SCItem sourceItem, Guid destParentId)
		{
			return new SCItem()
			{
				ID = sourceItem.ID,
				Name = sourceItem.Name,
				MasterID = Guid.Empty,
				ParentID = destParentId,
				Created = sourceItem.Created,
				Updated = sourceItem.Updated,
				TemplateID = _itemMetadataTemplate.destTemplateId,
				Fields = ConvertFields(sourceItem.Fields)
			};
		}

		private List<SCField> ConvertFields(List<SCField> fields)
		{
			var destFields = new List<SCField>();

			var itemId = fields.First().ItemId;

			IEnumerable<Tuple<string,int>> langVersions = fields.Where(f => f.Version != null && f.Language != null).Select(f => new Tuple<string,int>(f.Language, (int)f.Version)).Distinct();
			var languages = fields.Where(f => f.Language != null).Select(f => f.Language).Distinct();

			var filteredSourceFields = fields.Where(f =>
				_itemMetadataTemplate.fields.existingFields.Select(mf => mf.fieldId).Contains(f.FieldId));

			foreach (var filteredSourceField in filteredSourceFields)
			{
				SCField destField = null;
				IFieldConverter converter;
				var existingField =
					_itemMetadataTemplate.fields.existingFields.FirstOrDefault(mf => mf.fieldId == filteredSourceField.FieldId);

				if (existingField == null)
					continue;

				if(!string.IsNullOrEmpty(existingField.fieldConverter))
				{
					converter = ConverterInstantiator.CreateInstance(_appSettings.converters.FirstOrDefault(c => c.name == existingField.fieldConverter)?.converterType);
					destField = converter?.Convert(filteredSourceField);
				}
				else
				{
					destField = filteredSourceField;
				}

				if (destField != null && destField.FieldId != Guid.Empty)
				{
					destFields.Add(destField);
				}
			}

			foreach (var newField in _itemMetadataTemplate.fields.newFields)
			{
				SCField destField = null;

				var fieldValue = GetValue(newField.value, newField.valueType);

				switch (newField.fieldType)
				{
					case FieldType.Shared:
						destField = _fieldFactory.CreateSharedField(newField.fieldId, itemId, fieldValue);
						if (destField != null)
						{
							destFields.Add(destField);
						}
						break;
					case FieldType.Versioned:
						foreach (var langVersion in langVersions)
						{
							destField = _fieldFactory.CreateVersionedField(newField.fieldId, itemId, fieldValue, langVersion.Item2, langVersion.Item1);
							if (destField != null)
							{
								destFields.Add(destField);
							}
						}
						break;
					case FieldType.Unversioned:
						foreach (var language in languages)
						{
							destField = _fieldFactory.CreateUnversionedField(newField.fieldId, itemId, fieldValue, language);
							if (destField != null)
							{
								destFields.Add(destField);
							}
						}
						break;
					//default:
						//throw new ArgumentOutOfRangeException(); TODO: To implement meanful error message
				}
			}
			
			return destFields;
		}

		private string GetValue(string value, string valueType)
		{
			return value ?? GenerateValue(valueType);
		}

		private string GenerateValue(string valueType)
		{
			var value = string.Empty;
			switch (valueType.ToLower())
			{
				case "system.datetime":
					value = DateTime.UtcNow.ToString("yyyyMMddThhmmssZ");
					break;
				case "system.guid":
					value = Guid.NewGuid().ToString();
					break;
				case "system.string":
					value = Guid.NewGuid().ToString("N").ToUpper();
					break;
				default:
					value = string.Empty;
					break;
			}

			return value;
		}
	}
}
