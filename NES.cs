using Godot;
using System;
using System.Runtime.Intrinsics.Arm;


public partial class NES : MeshInstance3D
{
    private CPU cpu;
    private PPU ppu;
    private ROM rom;
    public ShaderMaterial nesSpriteMaterial;

    [Export]
    private int hoge;
    private NesController joypad1;
    private NesController joypad2;

	public override void _Ready()
	{
        var displayMat = this.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
        var romPath = "smb.nes";
        rom = new ROM(romPath);
        joypad1 = new NesController(reset);
        joypad2 = new NesController(reset);
        ppu = new PPU(this, rom, displayMat, nesSpriteMaterial);
        cpu = new CPU(ppu, joypad1, joypad2);
    }

    private void reset()
    {
        cpu.SoftReset();
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
    {
        if (cpu == null) return;
        //for (var y = 0; y < 240; y++)
        //{
        //    for (var x = 0; x < 256; x++)
        //    {
        //        ppu.drawBackground(x, y);
        //        //if (showSprites == 1)
        //        //{
        //        //    //drawSprite(i, scanline);
        //        //}
        //    }
        //}

        joypad1.getInput();
        joypad2.getInput();

        long _c = 0;
        while (_c < 29780)
        {
            var c = cpu.Step();
            _c += c;
            for (var i = 0; i < c * 3; i++)
            {
                ppu.Step();
            }
        }

        ppu.updateSprites();
        //ppu.render();
    }

    public override void _Input(InputEvent evt)
    {
        //joypad1.getInput(evt);
        //joypad2.getInput(evt);
    }

}
