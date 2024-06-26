using Godot;
using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;



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

	public int HeaderEndByte;
	public double _unixTime;
	public string MapName; // TODO: Unused right now 

	public Image _screencap;


	public SaveHeader(Image image)
	{
		_screencap = image;
		
		_screencap.Resize(_screencapX, _screencapY);
		_screencap.Compress(Image.CompressMode.S3Tc, Image.CompressSource.Generic);
		_unixTime = Time.GetUnixTimeFromSystem();
		MapName = "";
		HeaderEndByte = 0;
	}

	public ImageTexture ScreencapTexture()
	{
		ImageTexture image = new ImageTexture();
        image.SetImage(_screencap);
		return image;
	}

	public void Write(FileAccess file)
	{
		file.Store32(55);
		file.StoreVar(_unixTime);
		file.StoreVar(_screencap, true);
	}

	public void Read(FileAccess file)
	{
		file.Get32();
		_unixTime  = (double)file.GetVar();
		_screencap = (Image)file.GetVar(true);
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
		GD.Print($"\n{objType}");
		properties.All(prop => {GD.Print(prop.Name ); return true;});
		fields.All(field =>    {GD.Print(field.Name); return true;});
		
	
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

		return buff;
	}
}


public partial class Game : Node
{	
	[Signal] public delegate void SaveLoadedEventHandler(string saveName);
	[Signal] public delegate void SaveMadeEventHandler(string saveName);

	static Type _saveAttrib = typeof(SaveAttribute);
	static string _saveDir = "save"; 
	static string _saveExt = "krg"; 
	static string SavePath(string name) => $"{_saveDir}/{name}.{_saveExt}";

	Texture2D _cachedScreenshot;

	Node player;
	Node map;

	public GameStatus Status = GameStatus.Unloaded;

	// Write this to header?
	List<SaveObject> _saveList = new List<SaveObject>();
	int _saveSize;

	
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
		CreateSaveObjects(this, this);
		/*GD.Print("\nSave Nodes:______");
		foreach(var n in _saveList)
		{
			GD.Print(n.Name);
		}*/
		
		/*
		GD.Print("\nBefore: ");
		GD.Print(((Player)_saveList[0]).findMeBaby);

		object obj = _saveList[0];
		((Player)obj).findMeBaby = 88;

		GD.Print("After: ");
		GD.Print(((Player)_saveList[0]).findMeBaby);
		var fuck = this;
		switch(fuck)
		{
			case Node node:
			
			break;
		}*/
	}

	public void MakeSave(string name)
	{
		string savePath = SavePath(name);

		int byteOffset = 0;
		using(var file = FileAccess.Open(savePath, FileAccess.ModeFlags.Write))
		{
			CacheScreencap();
			SaveHeader header = new SaveHeader(_cachedScreenshot.GetImage());
			
			// Write header, then use file pos to get size of header & write it back to the begining of the file.
			header.Write(file);
			byteOffset = (int)file.GetPosition(); // This is some hacky shit. I blame C#. Why is it so difficult to get the size of something? YOU CLEARY KNOW IT C# JUST TELL ME -_-
			file.Seek(0);
			file.Store32((uint)byteOffset);
			file.Seek((ulong)byteOffset);

			for(int i = 0; i < _saveList.Count; i++)
			{
				file.StoreBuffer(_saveList[i].Data());	
				byteOffset += _saveList[i].ByteSize;
			}
		}

		GD.Print("Saved.");
	}

	public Error LoadSave(string name)
	{	
		string savePath = SavePath(name);
		if(!FileAccess.FileExists(savePath)){
			return Error.FileNotFound;
		}

		// Load file as raw bytes
		byte[] file = FileAccess.GetFileAsBytes(savePath);
		if(file.Length == 0 || file == null){
			return FileAccess.GetOpenError();
		}

		// Load Header data
		int byteOffset = BitConverter.ToInt32(file, 0);

		for(int i = 0; i < _saveList.Count; i++)
		{
			for(int j = 0; j < _saveList[i].Properties.Length; j++)
			{
				// Get value from obj inst to use for pattern matching switch. <rant> My hand is forced. Why the hell can't we just switch on types??? </rant>
				var obj  = _saveList[i].Obj;
				var prop = _saveList[i].Properties[j];
				var value = prop.GetValue(_saveList[i].Obj);
				
				switch(value)
				{
					case Variant:
					//prop.SetValue(obj, v);
					GD.Print("Used a variant... is this okay?");
					break;

					case int:
					int v = BitConverter.ToInt32(file, byteOffset);
					prop.SetValue(obj, v);
					byteOffset += 4;
					break;

					case float:
					break;

					case double:
					break;

					default:
					GD.PrintErr($"Property type not supported: {prop.PropertyType}");
					break;
				}
			}
		}

		//GetNode<TextureRect>("img").Texture = header.ScreencapTexture();
		GD.Print("Loaded.");
		Status = GameStatus.Loaded;//??
		EmitSignal(SignalName.SaveLoaded);
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

		SaveHeader header;
		using(var file = FileAccess.Open(savePath, FileAccess.ModeFlags.Read))
		{
			GD.Print();
			file.Get32();
			file.GetVar();
			var image = file.GetVar(true);

			header = new SaveHeader()
			{
				_screencap = (Image)image,

			};
			//header.Read(file);
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

	/// <summary>
	/// Recursive loop all Game's nodes & find any [Save] classes. Create SaveObjects from them 
	/// </summary>
	static void CreateSaveObjects(Game game, Node node)
	{
		SaveObject? saveObj = SaveObject.MakeFrom(node);
		if(saveObj.HasValue){
			game._saveList.Add(saveObj.Value);
			game._saveSize += saveObj.Value.ByteSize;

			GD.Print($"Node:{node.Name} saved.");
		}

		var children = node.GetChildren();
		if(children.Count == 0){
			return;
		}
		foreach(var child in children){
			CreateSaveObjects(game, child);	
		}
	}
}
