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


public partial class Game : Node
{	
	[Signal] public delegate void SaveLoadedEventHandler(string saveName);
	[Signal] public delegate void SaveMadeEventHandler(string saveName);

	static string _saveDir = "save"; 
	static string _saveExt = "krg"; 
	static string SavePath(string name) => $"{_saveDir}/{name}.{_saveExt}";

	Texture2D _cachedScreenshot;

	public GameStatus Status = GameStatus.Unloaded;

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
			byteOffset = (int)file.GetPosition(); // This is hacky. I blame C#. Why is it so difficult to get the size? YOU CLEARY KNOW IT C# JUST TELL ME WHAT ARE YOU THE IRS?!?!?!
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
