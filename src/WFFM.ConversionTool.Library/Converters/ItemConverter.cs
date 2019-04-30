﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using WFFM.ConversionTool.Library.Factories;
using WFFM.ConversionTool.Library.Helpers;
using WFFM.ConversionTool.Library.Models;
using WFFM.ConversionTool.Library.Models.Metadata;
using WFFM.ConversionTool.Library.Models.Sitecore;
using WFFM.ConversionTool.Library.Providers;
using WFFM.ConversionTool.Library.Repositories;

namespace WFFM.ConversionTool.Library.Converters
{
	public class ItemConverter : IItemConverter
	{
		private IFieldFactory _fieldFactory;
		private MetadataTemplate _itemMetadataTemplate;
		private AppSettings _appSettings;
		private IMetadataProvider _metadataProvider;
		private IItemFactory _itemFactory;
		private IDestMasterRepository _destMasterRepository;

		private readonly string _baseFieldConverterType = "WFFM.ConversionTool.Library.Converters.BaseFieldConverter, WFFM.ConversionTool.Library";

		public ItemConverter(IFieldFactory fieldFactory, AppSettings appSettings, IMetadataProvider metadataProvider, IItemFactory itemFactory, IDestMasterRepository destMasterRepository)
		{
			_fieldFactory = fieldFactory;
			_appSettings = appSettings;
			_metadataProvider = metadataProvider;
			_itemFactory = itemFactory;
			_destMasterRepository = destMasterRepository;
		}

		public List<SCItem> Convert(SCItem scItem, Guid destParentId)
		{
			_itemMetadataTemplate = _metadataProvider.GetItemMetadataByTemplateId(scItem.TemplateID);
			if (_itemMetadataTemplate.sourceMappingFieldId != null && _itemMetadataTemplate.sourceMappingFieldId != Guid.Empty)
			{
				var mappedMetadataTemplate = _metadataProvider.GetItemMetadataBySourceMappingFieldValue(scItem.Fields
					.FirstOrDefault(f => f.FieldId == _itemMetadataTemplate.sourceMappingFieldId)?.Value);
				if (mappedMetadataTemplate != null)
				{
					_itemMetadataTemplate = mappedMetadataTemplate;
				}
			}

			List<SCItem> destItems = ConvertItemAndFields(scItem, destParentId);
			return destItems;
		}

		private List<SCItem> ConvertItemAndFields(SCItem sourceItem, Guid destParentId)
		{
			List<SCItem> destItems = new List<SCItem>();
			List<SCItem> convertedItems = new List<SCItem>();

			var destItem = new SCItem()
			{
				ID = sourceItem.ID,
				Name = sourceItem.Name,
				MasterID = Guid.Empty,
				ParentID = destParentId,
				Created = sourceItem.Created,
				Updated = sourceItem.Updated,
				TemplateID = _itemMetadataTemplate.destTemplateId,
				Fields = sourceItem.Fields
			};

			// Create descendant items
			destItems.AddRange(_itemFactory.CreateDescendantItems(_itemMetadataTemplate, destItem));

			if (destItems.Any())
			{
				var lastDescendantItem = destItems.FirstOrDefault(item => !destItems.Select(i => i.ParentID).Contains(item.ID));
				convertedItems = ConvertFields(destItem, lastDescendantItem);
			}
			else
			{
				convertedItems = ConvertFields(destItem, null);
			}

			destItems.AddRange(convertedItems);

			return destItems;
		}

		private List<SCItem> ConvertFields(SCItem destItem, SCItem lastDescendantItem)
		{
			var destFields = new List<SCField>();
			var destItems = new List<SCItem>();

			var sourceFields = destItem.Fields;

			var itemId = sourceFields.First().ItemId;

			IEnumerable<Tuple<string, int>> langVersions = sourceFields.Where(f => f.Version != null && f.Language != null).Select(f => new Tuple<string, int>(f.Language, (int)f.Version)).Distinct();
			var languages = sourceFields.Where(f => f.Language != null).Select(f => f.Language).Distinct();

			// Migrate existing fields
			if (_itemMetadataTemplate.fields.existingFields != null)
			{
				var filteredExistingFields = sourceFields.Where(f =>
					_itemMetadataTemplate.fields.existingFields.Select(mf => mf.fieldId).Contains(f.FieldId));

				foreach (var filteredExistingField in filteredExistingFields)
				{
					var existingField =
						_itemMetadataTemplate.fields.existingFields.FirstOrDefault(mf => mf.fieldId == filteredExistingField.FieldId);

					if (existingField != null)
					{
						destFields.Add(filteredExistingField);
					}
				}
			}

			// Convert fields
			if (_itemMetadataTemplate.fields.convertedFields != null)
			{
				// Select only fields that are mapped
				var filteredConvertedFields = sourceFields.Where(f =>
					_itemMetadataTemplate.fields.convertedFields.Select(mf => mf.sourceFieldId).Contains(f.FieldId));

				foreach (var filteredConvertedField in filteredConvertedFields)
				{
					var convertedField =
						_itemMetadataTemplate.fields.convertedFields.FirstOrDefault(mf =>
							mf.sourceFieldId == filteredConvertedField.FieldId);

					if (convertedField != null)
					{
						// Process fields that have multiple dest fields
						if (convertedField.destFields != null && convertedField.destFields.Any())
						{
							var valueElements = XmlHelper.GetXmlElementNames(filteredConvertedField.Value);
							
							var filteredValueElementsToMany = convertedField.destFields.Where(f =>
								valueElements.Contains(f.sourceElementName.ToLower()) && f.destFieldId == null);

							foreach (var valueXmlElementMapping in filteredValueElementsToMany)
							{
								// Special case for List Datasource fields
								if (string.Equals(valueXmlElementMapping.sourceElementName, "Items",
									StringComparison.InvariantCultureIgnoreCase))
								{
									IFieldConverter converter = InitConverter(valueXmlElementMapping.fieldConverter);

									List<SCField> convertedFields = converter?.ConvertValueElementToFields(filteredConvertedField,
										XmlHelper.GetXmlElementValue(filteredConvertedField.Value, valueXmlElementMapping.sourceElementName));
									if (convertedFields != null && convertedFields.Any())
									{
										destFields.AddRange(convertedFields);
									}

									// Delete existing list items
									var listItemMetadataTemplate = _metadataProvider.GetItemMetadataByTemplateName("ExtendedListItem");
									if (lastDescendantItem != null)
									{
										var listItems =
											_destMasterRepository.GetSitecoreChildrenItems(listItemMetadataTemplate.destTemplateId,
												lastDescendantItem.ID);

										foreach (SCItem listItem in listItems)
										{
											_destMasterRepository.DeleteSitecoreItem(listItem);
										}
									}

									List<SCItem> convertedItems = converter?.ConvertValueElementToItems(filteredConvertedField,
										XmlHelper.GetXmlElementValue(filteredConvertedField.Value, valueXmlElementMapping.sourceElementName),
										listItemMetadataTemplate, lastDescendantItem ?? destItem);
									if (convertedItems != null && convertedItems.Any())
									{
										destItems.AddRange(convertedItems);
									}
								}
							}

							var filteredValueElements =
								convertedField.destFields.Where(f => valueElements.Contains(f.sourceElementName.ToLower()) && f.destFieldId != null);

							foreach (var valueXmlElementMapping in filteredValueElements)
							{
								IFieldConverter converter = InitConverter(valueXmlElementMapping.fieldConverter);

								SCField destField = converter?.ConvertValueElement(filteredConvertedField, (Guid)valueXmlElementMapping.destFieldId, XmlHelper.GetXmlElementValue(filteredConvertedField.Value, valueXmlElementMapping.sourceElementName), destItems);

								if (destField != null && destField.FieldId != Guid.Empty)
								{
									destFields.Add(destField);
								}
							}
						}
						// Process fields that have a single dest field
						else if (convertedField.destFieldId != null)
						{
							IFieldConverter converter = InitConverter(convertedField.fieldConverter);
							SCField destField = converter?.ConvertField(filteredConvertedField, (Guid)convertedField.destFieldId);

							if (destField != null && destField.FieldId != Guid.Empty)
							{
								destFields.Add(destField);
							}
						}
					}
				}
			}

			// Create new fields
			foreach (var newField in _itemMetadataTemplate.fields.newFields)
			{
				destFields.AddRange(_fieldFactory.CreateFields(newField, itemId, langVersions, languages));
			}

			destItem.Fields = destFields;
			destItems.Add(destItem);

			return destItems;
		}

		private IFieldConverter InitConverter(string converterName)
		{
			var converterType = _baseFieldConverterType;
			if (converterName != null)
			{
				var metaConverter = _appSettings.converters.FirstOrDefault(c => c.name == converterName)?.converterType;
				if (!string.IsNullOrEmpty(metaConverter))
				{
					converterType = metaConverter;
				}
			}
			return IoC.CreateInstance(converterType);
		}




	}

}
