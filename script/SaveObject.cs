using Godot;
using System;
using System.Linq;
using System.Data;
using System.Reflection;
using System.Runtime.InteropServices;


struct SaveObject
{
	public object Obj;
	
	public FieldInfo[] Fields;
	public PropertyInfo[] Properties;
	public int ByteSize;


    public static SaveObject? MakeFrom(object obj)
	{
		Type objType = obj.GetType();

		// Is object marked with [Save]?
		var attribs = Attribute.GetCustomAttributes(objType);
		bool hasSaveAttrib = attribs.Any(attrib => attrib.GetType() == typeof(SaveAttribute));
		if(!hasSaveAttrib){
			return null;
		}

		// Get properties marked with [Save]
		var properties = objType.GetRuntimeProperties();
		properties = properties.Where(prop => prop.CustomAttributes.Any(attrib => attrib.AttributeType == typeof(SaveAttribute)));

		// Get fields marked with [Save]
		var fields = objType.GetRuntimeFields();
		fields = fields.Where(field => field.CustomAttributes.Any(attrib => attrib.AttributeType == typeof(SaveAttribute)));

		// Discard if no [Save] members
		if(properties.Count() + fields.Count() <= 0){
			GD.Print($"\n{objType}: Marked with [Save] has no [Save] members. Discarding SaveObject");
			return null;
		}

		// Calc sum byte size of all [Save] members
		int byteSize = 0;
		properties.All(prop => {byteSize += Marshal.SizeOf(prop.PropertyType); return true;});
		fields.All(field    => {byteSize += Marshal.SizeOf(field.FieldType  ); return true;});

		// Debug print
		//GD.Print($"\n{objType}");
		//properties.All(prop => {GD.Print(prop.Name ); return true;});
		//fields.All(field =>    {GD.Print(field.Name); return true;});
	
		return new SaveObject()
		{
			Obj        = obj,
			Properties = properties.ToArray(),
			Fields     = fields.ToArray(),
			ByteSize   = byteSize
		};
	}

	public byte[] Data()
	{
		int byteOffset = 0;   
		byte[] buff = new byte[ByteSize];

		for(int i = 0; i < Properties.Length; i++)
		{
			var prop = Properties[i];
			var propByteSize = Marshal.SizeOf(prop.PropertyType);
			
			dynamic value = prop.GetValue(Obj);  
			byte[] bytes =  BitConverter.GetBytes(value);
			for(int j = 0; j < bytes.Length; j++)
			{
				buff[byteOffset + j] = bytes[j];
			}	 

			byteOffset += propByteSize;
		}

		for(int i = 0; i < Fields.Length; i++)
		{
			var field = Fields[i];
			var fieldByteSize = Marshal.SizeOf(field.FieldType);
			
			dynamic value = field.GetValue(Obj);  
			byte[] bytes =  BitConverter.GetBytes(value);
			for(int j = 0; j < bytes.Length; j++)
			{
				buff[byteOffset + j] = bytes[j];
			}	 

			byteOffset += fieldByteSize;
		}

		return buff;
	}
}
