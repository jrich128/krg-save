using Godot;
using System;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Used to tag Classes & their Fields for Saving
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property)]
public class SaveAttribute : Attribute
{
	public SaveAttribute()
	{
	}	
}
