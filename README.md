# Krg-Save
## C# Binary Serialization for Godot 4

## Features
- Binary saving via C# Attributes with zero boilerplate required
- No dependencies outside of Godot

## Purpose
This is intended to make saving as simple and efficient as possible. It is built to drop into any class with no boilerplate. Have your nodes to save be under a <code>Game</code> node, have any classes with data to save marked with <code>[Save]</code>, and any of their variables you wish to save also marked with <code>[Save]</code>. It saves only binary data; No bloat.

Aside from any code you might need to run on Save loaded to reinitialize, the <code>[Save]</code> Attribute is all you need to add.

## Usage
1. Have the root of your scene you wish to save be type <code>Game</code>
2. Have any C# classes that you want saved marked with the <code>[Save]</code> Attribute
3. Have any variables to save marked with the <code>[Save]</code> Attribute as well
4. Call <code>Game.MakeSave(string name)</code>, it will write all data you have marked to a file in a folder in your project root named "save" by default
5. Call <code>Game.LoadSave(string name)</code> to return the scene the saved state

Example:
```
[Save]
public partial class Wallet : Node
{
    [Save]
    int _money = 0;
}
```
For any code needed to re-init on loading a save, attach to Game's <code>SaveLoaded</code> Signal:
```
[Save]
public partial class Wallet : Node
{
    Game _game;

    [Save]
    int _money = 0;
    

    public override void _Ready()
    {
        _game = (Game)Owner;
        _game.Connect(Game.SignalName.SaveLoaded, new Callable(this, "OnSaveLoad"));
    }

    void OnSaveLoad(string name)
    {
        // init code here
    }
}
```

Alternativity, you can use getters and setters like this to avoid needing Game's Signal:
```
[Save]
public partial class Lamp : Node
{
    OmniLight3D _light;
    
    [Save]
    public bool On
    {
        get => _light.Visible;
        set => _light.Visible = value;
    } 
    

    public override void _Ready()
    {
        _light = GetNode<OminLight3D>("light");
    }
}
```



### Use case & Limitations
- This approach assumes that your Game scene has an unchanging number of nodes to be saved, and that they are always in the same order.
- Changing number of <code>[Save]</code> variables will render older save data incompatible. 

Although it is functional, this is still a WIP.

You can reach me at jrich128@proton.me 
