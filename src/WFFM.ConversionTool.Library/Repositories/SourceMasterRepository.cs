﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WFFM.ConversionTool.Library.Database.Master;
using WFFM.ConversionTool.Library.Models;
using WFFM.ConversionTool.Library.Models.Sitecore;

namespace WFFM.ConversionTool.Library.Repositories
{
	public class SourceMasterRepository : ISourceMasterRepository
	{
		private SourceMasterDb _sourceMasterDb;

		public SourceMasterRepository(SourceMasterDb sourceMasterDb)
		{
			_sourceMasterDb = sourceMasterDb;
		}

		public SCItem GetSitecoreItem(Guid itemId)
		{
			return GetSourceItemAndFields(_sourceMasterDb.Items.FirstOrDefault(item => item.ID == itemId));
		}

		public List<SCItem> GetSitecoreItems(Guid templateId)
		{
			var items = GetItems(templateId);
			List<SCItem> scItems = new List<SCItem>();
			foreach (var item in items)
			{
				scItems.Add(GetSourceItemAndFields(item));
			}

			return scItems;
		}

		public bool ItemHasChildrenOfTemplate(Guid templateId, SCItem scItem)
		{
			return _sourceMasterDb.Items.Any(item => item.TemplateID == templateId && item.ParentID == scItem.ID);
		}

		public List<SCItem> GetSitecoreChildrenItems(Guid templateId, Guid parentId)
		{
			var childrenItems = GetChildrenItems(templateId, parentId);
			List<SCItem> scItems = new List<SCItem>();
			foreach (var item in childrenItems)
			{
				scItems.Add(GetSourceItemAndFields(item));
			}

			return scItems;
		}

		public string GetSitecoreItemName(Guid itemId)
		{
			return _sourceMasterDb.Items.FirstOrDefault(item => item.ID == itemId)?.Name;
		}

		public string GetItemPath(Guid itemId)
		{
			var scItem = _sourceMasterDb.Items.FirstOrDefault(item => item.ID == itemId);
			if (scItem == null)
			{
				return string.Empty;
			}

			List<string> itemNames = new List<string>();
			itemNames.Add(scItem.Name);
			var isRootSitecoreItem = false;
			while (!isRootSitecoreItem)
			{
				scItem = _sourceMasterDb.Items.FirstOrDefault(item => item.ID == scItem.ParentID);
				if (scItem == null) break;
				itemNames.Add(scItem.Name);
				isRootSitecoreItem = scItem.ID == new Guid("{11111111-1111-1111-1111-111111111111}");		
			}

			itemNames.Reverse();
			return "/" + String.Join("/", itemNames);
		}

		public Guid GetItemTemplateId(Guid itemId)
		{
			return _sourceMasterDb.Items.FirstOrDefault(item => item.ID == itemId)?.TemplateID ?? Guid.Empty;
		}

		public byte[] GetSitecoreBlobData(Guid blobId)
		{
			var blobRecord = _sourceMasterDb.Blobs.FirstOrDefault(blob => blob.BlobId == blobId);

			return blobRecord?.Data;
		}

		private SCItem GetSourceItemAndFields(Item sourceItem)
		{
			return new SCItem()
			{
				ID = sourceItem.ID,
				Name = sourceItem.Name,
				MasterID = sourceItem.MasterID,
				ParentID = sourceItem.ParentID,
				TemplateID = sourceItem.TemplateID,
				Created = sourceItem.Created,
				Updated = sourceItem.Updated,
				Fields = GetItemFields(sourceItem.ID),
			};
		}

		/// <summary>
		/// Get the list of existing items by templateId in source master database
		/// </summary>
		/// <returns></returns>
		private List<Item> GetItems(Guid templateId)
		{
			return _sourceMasterDb.Items.Where(item => item.TemplateID == templateId && item.Name != "__Standard Values").ToList();
		}

		/// <summary>
		/// Get the list of existing children items of a specific template of a parent item in source master database
		/// </summary>
		/// <param name="templateId"></param>
		/// <param name="parentId"></param>
		/// <returns></returns>
		private List<Item> GetChildrenItems(Guid templateId, Guid parentId)
		{
			return _sourceMasterDb.Items.Where(item => item.TemplateID == templateId && item.Name != "__Standard Values" && item.ParentID == parentId).ToList();
		}

		/// <summary>
		/// Get list of fields of a Sitecore item from the source master database
		/// </summary>
		private List<SCField> GetItemFields(Guid itemId)
		{
			// fields from item template
			var fields = _sourceMasterDb.SharedFields.Where(field => field.ItemId == itemId)
				.Select(field => new SCField() { Id = field.Id, Value = field.Value, Created = field.Created, Updated = field.Updated, ItemId = field.ItemId, Type = FieldType.Shared, Language = null, Version = null, FieldId = field.FieldId })
				.Union(_sourceMasterDb.UnversionedFields.Where(field => field.ItemId == itemId)
					.Select(field => new SCField() { Id = field.Id, Value = field.Value, Created = field.Created, Updated = field.Updated, ItemId = field.ItemId, Type = FieldType.Unversioned, Language = field.Language, Version = null, FieldId = field.FieldId }))
				.Union(_sourceMasterDb.VersionedFields.Where(field => field.ItemId == itemId)
					.Select(field => new SCField() { Id = field.Id, Value = field.Value, Created = field.Created, Updated = field.Updated, ItemId = field.ItemId, Type = FieldType.Versioned, Language = field.Language, Version = field.Version, FieldId = field.FieldId }));

			return fields.ToList();
		}
	}
}
