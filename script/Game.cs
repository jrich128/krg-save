using Godot;
using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/*
	TODO: Replace the binary parsing code with using 
	MemoryStream & BinaryWrite / Read 
*/

public enum GameStatus
{
	Unloaded,
	Loaded
}

[GlobalClass]
public partial class Game : Node
{	
	[Signal] public delegate void SaveLoadedEventHandler(string saveName);
	[Signal] public delegate void SaveMadeEventHandler(string saveName);

	static string _saveDir = "save"; 
	static string _saveExt = "krg"; 
	static string SavePath(string name) => $"{_saveDir}/{name}.{_saveExt}";

	public Texture2D CachedScreenshot;
	public void CaptureCachedSreenshot()
	{
		CachedScreenshot = GetViewport().GetTexture();
	}

	public GameStatus Status = GameStatus.Unloaded;

	List<SaveObject> _saveList = new List<SaveObject>();
	int _saveSize;


	public override void _Ready()
	{	
		// Check for save dir, make if none
		if(!DirAccess.DirExistsAbsolute(_saveDir)){
			var err = DirAccess.MakeDirAbsolute(_saveDir);
			if(err != Error.Ok){
				GD.PrintErr($"Failed making save dir: '{err}'");
				return;
			}
		}

		CreateSaveObjects(this, this);
	}

	public void MakeSave(string name)
	{
		string savePath = SavePath(name);

		int byteOffset = 0;
		using(var file = FileAccess.Open(savePath, FileAccess.ModeFlags.Write))
		{
			CaptureCachedSreenshot();
			SaveHeader header = new SaveHeader(CachedScreenshot.GetImage());
			
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
					case int:
					prop.SetValue(obj, BitConverter.ToInt32(file, byteOffset));
					byteOffset += sizeof(int);
					break;

					case float:
					prop.SetValue(obj, BitConverter.ToSingle(file, byteOffset));
					byteOffset += sizeof(float);
					break;

					case double:				
					prop.SetValue(obj, BitConverter.ToDouble(file, byteOffset));
					byteOffset += sizeof(double);
					break;

					case bool:		
					GD.Print("bool");		
					prop.SetValue(obj, BitConverter.ToBoolean(file, byteOffset));
					byteOffset += sizeof(bool);
					break;

					default:
					GD.PrintErr($"Property type not supported: {prop.PropertyType}");
					break;
				}
			}

			for(int j = 0; j < _saveList[i].Fields.Length; j++)
			{
				// Get value from obj inst to use for pattern matching switch. <rant> My hand is forced. Why the hell can't we just switch on types??? </rant>
				var obj  = _saveList[i].Obj;
				var field = _saveList[i].Fields[j];
				var value = field.GetValue(_saveList[i].Obj);
		

				switch(value)
				{
					case int:
					field.SetValue(obj, BitConverter.ToInt32(file, byteOffset));
					byteOffset += sizeof(int);
					break;

					case float:
					field.SetValue(obj, BitConverter.ToSingle(file, byteOffset));
					byteOffset += sizeof(float);
					break;

					case double:				
					field.SetValue(obj, BitConverter.ToDouble(file, byteOffset));
					byteOffset += sizeof(double);
					break;

					case bool:		
					GD.Print("bool");		
					field.SetValue(obj, BitConverter.ToBoolean(file, byteOffset));
					byteOffset += sizeof(bool);
					break;

					default:
					GD.PrintErr($"Field type not supported: {field.FieldType}");
					break;
				}
			}
		}

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
	/// Recursive loop all Game's nodes & find any [Save] classes. Create SaveObjects from them 
	/// </summary>
	static void CreateSaveObjects(Game game, Node node)
	{
		SaveObject? saveObj = SaveObject.MakeFrom(node);
		if(saveObj.HasValue){
			game._saveList.Add(saveObj.Value);
			game._saveSize += saveObj.Value.ByteSize;
			//GD.Print($"Node:{node.Name} saved.");
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
