using Godot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;


public enum GameStatus
{
	Unloaded,
	Loaded
}

/// <summary>
/// Save game meta data
/// </summary>
public struct SaveHeader
{
	static int _screencapX = 480; // 1920x1080 / 4
    static int _screencapY = 270;

	Image _screencap;
	public double _unixTime;

	public string MapName; // TODO: Unused right now 


	public SaveHeader(Image image)
	{
		_screencap = image;
		
		_screencap.Resize(_screencapX, _screencapY);
		_screencap.Compress(Image.CompressMode.S3Tc, Image.CompressSource.Generic);
		_unixTime = Time.GetUnixTimeFromSystem();
		MapName = "";
	}

	public ImageTexture ScreencapTexture()
	{
		ImageTexture image = new ImageTexture();
        image.SetImage(_screencap);
		return image;
	}

	public void Write(FileAccess file)
	{
		file.StoreVar(MapName);
		file.StoreVar(_screencap, true);
		file.StoreVar(_unixTime);
	}

	public void Read(FileAccess file)
	{
		MapName    = (string)file.GetVar();
		_screencap = (Image)file.GetVar(true);
		_unixTime  = (double)file.GetVar();
	}

	public string TimeFormated()
	{
		// Offset UNIX timestamp for timezone. 
        // Bias in minutes, * 60 for seconds
		long timeZoneBias = (long)Time.GetTimeZoneFromSystem()["bias"] * 60;
		long adjustedTime = (long)_unixTime + timeZoneBias;

        var date = Time.GetDatetimeDictFromUnixTime(adjustedTime);

        return $"Date: {date["month"]}/{date["day"]}/{date["year"]}\nTime: {date["hour"]}:{date["minute"]}";
	}
}


public partial class Game : Node
{	
	static Type _saveAttrib = typeof(SaveAttribute);
	static string _saveDir = "save"; 
	static string _saveExt = "krg"; 
	static string SavePath(string name) => $"{_saveDir}/{name}.{_saveExt}";

	Texture2D _cachedScreenshot;

	Node player;
	Node map;

	public GameStatus Status = GameStatus.Unloaded;

	
	static Game()
	{ 	
		// Check for save dir, make if none
		if(!DirAccess.DirExistsAbsolute(_saveDir)){
			var err = Godot.DirAccess.MakeDirAbsolute(_saveDir);
			if(err != Error.Ok){
				GD.PrintErr($"Failed making save dir: '{err}'");
				return;
			}
		}
	}

	public override void _Ready()
	{	
		PrintAllNodes(this);
	}

	public void MakeSave(string name)
	{
		static void SaveFieldsAndProps(Node node, Type type, FileAccess file)
		{
			// Are any of node's properties marked with [Save]?
			var props  = type.GetRuntimeProperties();
			foreach(var prop in props)
			{
				// Is field marked with "[Save]" ?
				bool propHasSaveAttrib = prop.CustomAttributes.Any(attrib => attrib.AttributeType == _saveAttrib);
				if(!propHasSaveAttrib){
					continue;
				}
				// Is field of type "Variant"?
				if(prop.PropertyType != typeof(Variant)){
					GD.PrintErr($"Can only save type 'Variant'");
					continue;
				}

				Variant value = (Variant)prop.GetValue(node);
				file.StoreVar(value);
			}

			// Are any of node's fields marked with [Save]?
			var fields = type.GetRuntimeFields();
			foreach(var field in fields)
			{
				// Is field marked with "[Save]" ?
				bool fieldHasSaveAttrib = field.CustomAttributes.Any(attrib => attrib.AttributeType == _saveAttrib);
				if(!fieldHasSaveAttrib){
					continue;
				}
				// Is field of type "Variant"?
				if(field.FieldType != typeof(Variant)){
					GD.Print($"Can only save type 'Variant'");
					continue;
				}

				Variant value = (Variant)field.GetValue(node);
				file.StoreVar(value);
			}
		}

		// Recursivly loop through all nodes in Game & write their [save] data 
		static void Write(Node node, FileAccess file)
		{
			Type type = node.GetType();
			
			// Does node's Class have [Save]? 
			var attribs = Attribute.GetCustomAttributes(type);
			bool hasSaveAttrib = attribs.Any(attrib => attrib.GetType() == _saveAttrib);
			if(hasSaveAttrib){
				SaveFieldsAndProps(node, type, file);
				GD.Print($"Node:{node.Name} saved.");
			}

			var children = node.GetChildren();
			if(children.Count == 0){
				return;
			}
			foreach(var child in children){
				Write(child, file);
			}
		}

		string savePath = SavePath(name);

		using(var file = FileAccess.Open(savePath, FileAccess.ModeFlags.Write))
		{
			CacheScreencap();
			SaveHeader header = new SaveHeader(_cachedScreenshot.GetImage());
			
			header.Write(file);
			Write(this, file);
		}
	}

	public Error LoadSave(string name)
	{	
		static void LoadFieldsAndProps(Node node, Type type, FileAccess file)
		{
			// Are any of node's properties marked with [Save]?
			var props  = type.GetRuntimeProperties();
			foreach(var prop in props)
			{
				// Is field marked with "[Save]" ?
				bool propHasSaveAttrib = prop.CustomAttributes.Any(attrib => attrib.AttributeType == _saveAttrib);
				if(!propHasSaveAttrib){
					continue;
				}
				// Is field of type "Variant"?
				if(prop.PropertyType != typeof(Variant)){
					GD.Print($"Can only save type 'Variant'");
					continue;
				}

				Variant value = file.GetVar();
				prop.SetValue(node, value);
			}

			// Are any of node's fields marked with [Save]?
			var fields = type.GetRuntimeFields();
			foreach(var field in fields)
			{
				// Is field marked with "[Save]" ?
				bool fieldHasSaveAttrib = field.CustomAttributes.Any(attrib => attrib.AttributeType == _saveAttrib);
				if(!fieldHasSaveAttrib){
					continue;
				}
				// Is field of type "Variant"?
				if(field.FieldType != typeof(Variant)){
					GD.Print($"Can only save type 'Variant'");
					continue;
				}

				Variant value = file.GetVar();
				field.SetValue(node, value);
			}
		}

		// Recursivly loop through all nodes in Game & read their [save] data 
		static void Read(Node node, FileAccess file)
		{
			Type type = node.GetType();
			
			// Does node's Class have [Save]? 
			var attribs = Attribute.GetCustomAttributes(type);
			bool hasSaveAttrib = attribs.Any(attrib => attrib.GetType() == _saveAttrib);
			if(hasSaveAttrib){
				LoadFieldsAndProps(node, type, file);
				GD.Print($"Node:{node.Name} loaded.");
			}

			var children = node.GetChildren();
			if(children.Count == 0){
				return;
			}
			foreach(var child in children){
				Read(child, file);
			}
		}

		string savePath = SavePath(name);
		if(!FileAccess.FileExists(savePath)){
			return Error.FileNotFound;
		}

		SaveHeader header = new SaveHeader();
		using(var file = FileAccess.Open(savePath, FileAccess.ModeFlags.Read))
		{
			header.Read(file);
			Read(this, file);
		}

		GetNode<TextureRect>("img").Texture = header.ScreencapTexture();

		Status = GameStatus.Loaded;
		return Error.Ok;
	}

	/// <summary>
	/// Read & return only Save Game Header
	/// </summary>
	public static SaveHeader? Peek(string name)
	{
		string savePath = SavePath(name);
		if(!FileAccess.FileExists(savePath)){
			return null;
		}

		SaveHeader header = new SaveHeader();
		using(var file = FileAccess.Open(savePath, FileAccess.ModeFlags.Read))
		{
			header.Read(file);
		}

		return header;
	}

	/// <summary>
	/// Save screenshot when screen is clear of menus & whatnot 
	/// </summary>
	void CacheScreencap()
	{
		_cachedScreenshot = GetViewport().GetTexture();
	}

	static void PrintAllNodes(Node node)
	{
		if(node.Owner != null){
			GD.Print(node.Owner.Name);
		}

		var children = node.GetChildren();
		if(children.Count == 0){
			return;
		}
		foreach(var child in children){
			PrintAllNodes(child);	
		}
	}
}
