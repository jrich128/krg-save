using Godot;
using System;

/// <summary>
/// Save game meta data
/// </summary>
public struct SaveHeader
{
	static int _screencapX = 480; // 1920x1080 / 4
    static int _screencapY = 270;

	public int HeaderEndByte;
	public double UnixTime;
	public Image Screencap;


	public SaveHeader(Image image)
	{
		Screencap = image;
		Screencap.Resize(_screencapX, _screencapY);
		Screencap.Compress(Image.CompressMode.S3Tc, Image.CompressSource.Generic);

		UnixTime = Time.GetUnixTimeFromSystem();
		HeaderEndByte = 0;
	}

	public ImageTexture ScreencapTexture()
	{
		ImageTexture image = new ImageTexture();
        image.SetImage(Screencap);
		return image;
	}

	public void Write(FileAccess file)
	{
		file.Store32(0); // EndByte is unknown until we write. Finding the size of an Image is taboo, unless your the damn FileAccess class writing it. Go figure.
		file.StoreVar(UnixTime);
		file.StoreVar(Screencap, true);
	}

	public void Read(FileAccess file)
	{
		HeaderEndByte = (int)file.Get32(); 
		UnixTime  = (double)file.GetVar();
		Screencap = (Image)file.GetVar(true);
	}

	public string TimeFormated()
	{
		// Offset UNIX timestamp for timezone. 
        // Bias in minutes, * 60 for seconds
		long timeZoneBias = (long)Time.GetTimeZoneFromSystem()["bias"] * 60;
		long adjustedTime = (long)UnixTime + timeZoneBias;

        var date = Time.GetDatetimeDictFromUnixTime(adjustedTime);

        return $"Date: {date["month"]}/{date["day"]}/{date["year"]}\nTime: {date["hour"]}:{date["minute"]}";
	}
}
