#if TOOLS
using Godot;
using System;

[Tool]
public partial class KrgSavePlugin : EditorPlugin
{
	static string Dir          = "addons/krg-save";
	static string GameScript   = "script/Game.cs";
	static string TerminalIcon = "icon.png";
	static string Path(string fileName) => $"{Dir}/{fileName}";
	
	static string TypeName = "Game";
	static string FailText = "Plugin 'krg-save' failed to load.";


	public override void _EnterTree()
	{
		// Find plugin directory
		if(!DirAccess.DirExistsAbsolute(Dir)){
			GD.PrintErr($"{FailText}\nDirectory not found '{Dir}'");
			return;
        }

		// Find icon
		string iconPath = Path(TerminalIcon);
		if(!FileAccess.FileExists(iconPath)){
			GD.PrintErr($"{FailText}\nFile not found '{iconPath}'");
			return;
		}

		// Find script
		string scriptPath = Path(GameScript);
		if(!Godot.FileAccess.FileExists(scriptPath)){
			GD.PrintErr($"{FailText}\nFile not found '{scriptPath}'");
			return;
		}

		var icon   = ResourceLoader.Load<Texture2D>(iconPath);
		var script = ResourceLoader.Load<Script>(scriptPath);
		
		AddCustomType(TypeName, "Node", script, icon);
	}

	public override void _ExitTree()
	{
		RemoveCustomType(TypeName);
	}
}
#endif