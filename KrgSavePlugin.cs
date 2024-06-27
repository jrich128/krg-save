#if TOOLS
using Godot;
using System;

[Tool]
public partial class KrgSavePlugin : EditorPlugin
{
	static string PluginName = "krg-save";
    public override string _GetPluginName() => PluginName;

	static string Dir = $"addons/{PluginName}";
	

	public override void _EnterTree()
	{
		// Check for plugin directory
		if(!DirAccess.DirExistsAbsolute(Dir)){
			GD.PrintErr($"{PluginName}: Directory not found '{Dir}'");
			return;
        }

		string scriptPath = $"{Dir}/script/Game.cs";
		if(!FileAccess.FileExists(scriptPath)){
			GD.PrintErr($"{PluginName}: File not found '{scriptPath}'");
			return;
		}

		string iconPath = $"{Dir}/icon.png";
		if(!FileAccess.FileExists(iconPath)){
			GD.PrintErr($"{PluginName}: File not found '{iconPath}'");
			return;
		}
		
		var icon   = ResourceLoader.Load<Texture2D>(iconPath);
		var script = ResourceLoader.Load<Script>(scriptPath);
		
		AddCustomType("Game", "Node", script, icon);
	}

	public override void _ExitTree()
	{
		RemoveCustomType("Game");
	}
}
#endif