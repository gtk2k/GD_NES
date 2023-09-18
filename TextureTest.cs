using Godot;
using System;

public partial class TextureTest : MeshInstance3D
{
	private ImageTexture imgTex = new ImageTexture();
	private Image img = new Image();
	public override void _Ready()
	{
		img.Load("test.png");
		imgTex.SetImage(img);

		var mat = this.GetSurfaceOverrideMaterial(0);
        if(mat is StandardMaterial3D stdMat)
		{
            stdMat.AlbedoTexture = imgTex;
        }
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		//var rndX = new Random();
		//var rndY = new Random();
  //      img.SetPixel(rndX.Next(0, 500), rndY.Next(0, 500), new Color("white"));
  //      imgTex.SetImage(img);
    }
}
