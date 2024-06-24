# Features
- Binary Saving via C# Attributes
- Saving game screenshot within the save binary 
- No dependencies outside of Godot

# Usage
1. Have the root of your scene you wish to save be type <code>Game</code>
2. Have any C# classes that you want saved marked with the <code>[Save]</code> Attribute
3. Have any variables to save marked with the <code>[Save]</code> Attribute as well
4. Call <code>Game.MakeSave(string name)</code>, it will write all data you have marked to a file in a folder in your project root named "save" by default
5. Call <code>Game.LoadSave(string name)</code> to return the scene the saved state

Note that currently only type <code>Variant</code> fields & properties can be used. You must cast them to their proper types with <code>Variant.As<>()</code>

Example:
```
    [Save]
    public partial class Wallet : Node
    {
        [Save] Variant _count = 0;
    }
```

To avoid needing to cast within your scripts, you can do like this:
```
    int _count = 0;
    [Save] Variant Count{
        get => _count;
        set => _count = value.As<int>();
    }
```

This will be fixed to have proper typing soon

# Use case
This approach assumes that your Game scene has an unchanging number of nodes to be saved, and that they are always in the same order.

Changing number of fields & properties will render older save data incompatible. This is on the docket to fix as well.

Classes, fields, & properties not marked with <code>[Save]</code> don't effect anything

This is purpose written for a larger project I am working on; I hope others can find it useful too

# Contributions
I'm open to contributions. If you have any questions/concerns whatnot you can reach me at jrich128@proton.me