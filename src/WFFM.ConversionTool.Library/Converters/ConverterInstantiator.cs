﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WFFM.ConversionTool.Library.Converters
{
	public static class ConverterInstantiator
	{
		public static IFieldConverter CreateInstance(string converterType)
		{
			try
			{
				if (string.IsNullOrEmpty(converterType))
					return null;

				var parts = converterType.Split(',');
				var typeName = parts[0];
				var assemblyName = parts[1];
				return (IFieldConverter)Activator.CreateInstance(assemblyName, typeName).Unwrap();
			}
			catch (Exception ex)
			{
				// TODO: Add Logging
				return null;
			}			
		}
	}
}
