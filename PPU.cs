using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Godot;

public class PPU
{
    public Action OnNmi;

    // status
    public int spriteOverflow;
    public int sprite0Hit;
    public int vBlank;


    // control
    private int baseNameTable;
    private int increment;
    private int spritePatternTable;
    private int bgPatternTable;
    private int spriteSize;
    private int masterSlave;
    private int nmiEnable;

    // mask
    private int grayscale;
    private int showLeftBackground;
    private int showLeftSprites;
    private int showBackground;
    private int showSprites;
    private int redTint;
    private int greenTint;
    private int blueTint;

    private int oamAdr;
    public int[] oam = new int[64 * 4];

    public ROM rom;

    private readonly uint[] nesPalette = {
        0x7C7C7CFF, 0x0000FCFF, 0x0000BCFF, 0x4428BCFF, 0x940084FF, 0xA80020FF, 0xA81000FF, 0x881400FF, 0x503000FF, 0x007800FF, 0x006800FF, 0x005800FF, 0x004058FF, 0x000000FF, 0x000000FF, 0x000000FF,
        0xBCBCBCFF, 0x0078F8FF, 0x0058F8FF, 0x6844FCFF, 0xD800CCFF, 0xE40058FF, 0xF83800FF, 0xE45C10FF, 0xAC7C00FF, 0x00B800FF, 0x00A800FF, 0x00A844FF, 0x008888FF, 0x000000FF, 0x000000FF, 0x000000FF,
        0xF8F8F8FF, 0x3CBCFCFF, 0x6888FCFF, 0x9878F8FF, 0xF878F8FF, 0xF85898FF, 0xF87858FF, 0xFCA044FF, 0xF8B800FF, 0xB8F818FF, 0x58D854FF, 0x58F898FF, 0x00E8D8FF, 0x787878FF, 0x000000FF, 0x000000FF,
        0xFCFCFCFF, 0xA4E4FCFF, 0xB8B8F8FF, 0xD8B8F8FF, 0xF8B8F8FF, 0xF8A4C0FF, 0xF0D0B0FF, 0xFCE0A8FF, 0xF8D878FF, 0xD8F878FF, 0xB8F8B8FF, 0xB8F8D8FF, 0x00FCFCFF, 0xF8D8F8FF, 0x000000FF, 0x000000FF
    };


    public Texture2D paletteTexture;

    private int t;
    private int x;
    private int v;
    private int w;

    private int buf;

    private int scrollX, scrollY;

    private int cycle;
    private int scanline;

    private int[] mem = new int[0x10000];
    private int adr;

    public Func<int, int>[] bus;

    //private GameObject[] sprites;
    //private Texture2D spritePaletteTex;
    //private Color32[] spritePalettePixel;

    //public Texture2D nameTableTex = new Texture2D(512, 480, TextureFormat.ARGB32, false);
    //private Color32[] nameTablePix;

    //public Image paletteTex = new Image(64, 1, TextureFormat.ARGB32, false);
    //private Color32[] palettePix;

    //public Texture2D bgPaletteTex = new Texture2D(16, 1, TextureFormat.ARGB32, false);
    //private Color32[] bgPalettePix;

    //public Texture2D spPaletteTex = new Texture2D(16, 1, TextureFormat.ARGB32, false);
    //private Color32[] spPalettePix;

    ////private Texture2D nameTableTex = new Texture2D(512, 480, TextureFormat.ARGB32, false);
    ////private Color32[] nameTablePix;

    public ImageTexture patternTex = new ImageTexture();
    public Image patternImg = Image.Create(256, 128, false, Image.Format.Rgba8);
    private byte[] patternPix = new byte[256 * 128 * 4];

    public ImageTexture displayTex = new ImageTexture();
    public Image displayImg = Image.Create(256, 240, false, Image.Format.Rgba8);
    private byte[] displayPix = new byte[256 * 240 * 4];

    //private GameObject[] nameTableGOs;
    private MeshInstance3D[] spriteGOs = new MeshInstance3D[64];
    private ShaderMaterial[] spriteMats = new ShaderMaterial[64];

    private Color[] palette = new Color[64];
    private int[,] ptn = new int[512, 64];

    private Color[] cols = new[] { new Color(0, 0, 0, 0), new Color(0, 0, 1, 1), new Color(0, 1, 0, 1), new Color(1, 0, 0, 1) };

    private int tmp;

    private string logPath;

    private MeshInstance3D nes;

    public PPU(NES _nes, ROM _rom, StandardMaterial3D displayMat, Material nesSpriteMaterial)
    {
        logPath = "ppu2.log";
        if (File.Exists(logPath))
        {
            File.Delete(logPath);
        }
        nes = _nes;
        rom = _rom;

        scanline = -1;
        cycle = 0;

        displayTex.SetImage(displayImg);
        displayMat.AlbedoTexture = displayTex;

        createPalette();
        //updatePalleteTex();
        updatePatternTableTex();

        var debugDispMat = nes.GetParent().GetNode<MeshInstance3D>("DebugDisplay").GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
        debugDispMat.AlbedoTexture = patternTex;

        var nesShader = GD.Load<Shader>("res://NesSpriteShader.gdshader");

        var main = nes.GetTree().CurrentScene;
        for (var i = 0; i < 64; i++)
        {
            var nd = new MeshInstance3D();
            nd.Mesh = new QuadMesh();
            var mat = spriteMats[i] = new ShaderMaterial();
            nd.SetSurfaceOverrideMaterial(0, mat);
            mat.Shader = nesShader;
            nd.Scale = new Vector3(8f / 256f, 8f / 240f, 1f);
            nes.AddChild(nd);
            spriteGOs[i] = nd;
            //nes.GetParent().AddChild(nd);
            mat.SetShaderParameter("_patternTex", patternTex);
        }

        bus = new Func<int, int>[] { _2000, _2001, _2002, _2003, ___, _2005, _2006, _2007 };
    }

    private void Reset()
    {
        scanline = -1;
        cycle = 0;

        spriteOverflow = 0;
        sprite0Hit = 0;
        vBlank = 0;

        baseNameTable = 0;
        increment = 0;
        spritePatternTable = 0;
        bgPatternTable = 0;
        spriteSize = 0;
        masterSlave = 0;
        nmiEnable = 0;

        grayscale = 0;
        showLeftBackground = 0;
        showLeftSprites = 0;
        showBackground = 0;
        showSprites = 0;
        redTint = 0;
        greenTint = 0;
        blueTint = 0;

        oam = new int[64 * 4];
        oamAdr = 0;

        scrollX = 0;
        scrollY = 0;

        adr = 0;
    }

    private int read(int adr)
    {
        return adr switch
        {
            < 0x2000 => rom.read(adr, true),
            0x3f10 => mem[0x3f00],
            0x3f14 => mem[0x3f04],
            0x3f18 => mem[0x3f08],
            0x3f1C => mem[0x3f0C],
            < 0x10000 => mem[adr],
            _ => 0
        };
    }

    private void write(int adr, int value)
    {
        if (adr == 0x3f10 || adr == 0x3f14 || adr == 0x3f18 || adr == 0x3f1c)
        {
            GD.PrintS(adr.ToString("X4"), value.ToString("X2"));
            adr -= 0x10;
        }
        mem[adr] = value;
    }

    private long frameCnt = 0;

    public void Step()
    {

        if (scanline == -1 && cycle == 1)
        {
            spriteOverflow = 0;
            sprite0Hit = 0;
            vBlank = 0;
        }

        if (scanline == 241)
        {
            frameCnt++;

            if (cycle == 1)
            {
                displayImg.SetData(256, 240, false, Image.Format.Rgba8, displayPix);
                displayTex.SetImage(displayImg);

                vBlank = 1;
                if (nmiEnable == 1)
                {
                    OnNmi?.Invoke();
                }
            }
        }


        if (detectSprite0Hit(cycle, scanline))
        {
            sprite0Hit = 1;
        }

        cycle++;

        if (cycle > 340)
        {
            if (scanline >= 0 && scanline < 240 && cycle == 341)
            {
                for (var i = 0; i < 256; i++)
                {
                    drawBackground(i, scanline);
                    if (showSprites == 1)
                    {
                        //drawSprite(i, scanline);
                    }
                }
            }
            cycle = 0;
            scanline++;
            if (scanline > 260)
            {
                scanline = -1;
            }
        }
    }

    public void updateSprites()
    {
        var spritePalette = Enumerable.Range(0, 15).Select(i => palette[read(0x3f10 + i)]).ToArray();
        var st = spritePatternTable == 0 ? 0 : 1;
        for (var i = 0; i < 64; i++)
        {
            var spX = oam[i * 4 + 3];
            var spY = oam[i * 4 + 0];
            spriteGOs[i].GlobalPosition = new Vector3(spX - 124, 119 - spY - 3, 0.1f);

            var mat = spriteMats[i];
            mat.SetShaderParameter("_PatternTex", patternTex);
            mat.SetShaderParameter("_Sprite_B2", oam[i * 4 + 2]);
            mat.SetShaderParameter("_SpriteTable", spritePatternTable);
            mat.SetShaderParameter("_Pattern", oam[i * 4 + 1]);
            mat.SetShaderParameter("_Sprite_Palette", spritePalette);
        }
    }

    private int isBG = 0;
    private Color transparentColor = new Color(0, 0, 0, 0);
    public void drawBackground(int x, int y)
    {
        var _x = x;
        var _y = y;
        x += scrollX;
        y += scrollY;
        var bgPX = (7 - (x & 0x7));
        var bgPY = y & 0x7;
        x >>= 3;
        y >>= 3;

        var ntAdr = baseNameTable;
        if (x >= 32)
        {
            ntAdr ^= 0x400;
        }
        if (y >= 30) ntAdr ^= 0x800;

        x %= 32;
        y %= 30;

        var bgPtnNo = read(ntAdr + (y << 0x5) + x);
        var bgPtnAdrLo = bgPatternTable + (bgPtnNo << 0x4) + bgPY;
        var bgPtnAdrHi = bgPtnAdrLo + 0x8;
        var bgPtn = (((read(bgPtnAdrHi) & (1 << bgPX)) << 1) | (read(bgPtnAdrLo) & (1 << bgPX))) >> bgPX;

        var pxB = x >> 2;
        var pyB = y >> 2;
        var pB = (pyB << 3) | pxB;
        var palByte = read(ntAdr + 0x3c0 + pB);
        var pxO = (x >> 1) & 1;
        var pyO = (y >> 1) & 1;
        var offset = (pyO << 1) | pxO;
        var palHi = (palByte >> (offset << 1)) & 0x3;
        var tmp = (palHi << 2) | bgPtn;
        if (tmp == 4 || tmp == 8 | tmp == 0xC) tmp = 0;
        isBG = tmp == 0 ? 0 : 1;
        var bgPal = palette[read(0x3f00 + tmp)];

        var idx = _y * (256 * 4) + (_x * 4);
        //if (bgPtn == 0)
        //{
        //    displayPix[idx + 0] = 0;
        //    displayPix[idx + 1] = 0;
        //    displayPix[idx + 2] = 0;
        //    displayPix[idx + 3] = 0;
        //}
        //else
        //{
            displayPix[idx + 0] = (byte)bgPal.R8;
            displayPix[idx + 1] = (byte)bgPal.G8;
            displayPix[idx + 2] = (byte)bgPal.B8;
            displayPix[idx + 3] = (byte)bgPal.A8;
        //}
    }

    private bool detectSprite0Hit(int x, int y)
    {
        var sp0_X = oam[3];
        if (sp0_X >= x || sp0_X + 8 < x) return false;
        var sp0_Y = oam[0] + 1;
        if (sp0_Y >= y || sp0_Y + 8 < y) return false;

        var _x = x;
        var _y = y;
        x += scrollX;
        y += scrollY;
        var bgPX = (7 - (x & 0x7));
        var bgPY = y & 0x7;
        x >>= 3;
        y >>= 3;

        var ntAdr = baseNameTable;
        if (x >= 32)
        {
            ntAdr ^= 0x400;
        }
        if (y >= 30) ntAdr ^= 0x800;

        x %= 32;
        y %= 30;

        var bgPtnNo = read(ntAdr + (y << 0x5) + x);
        var bgPtnAdrLo = bgPatternTable + (bgPtnNo << 0x4) + bgPY;
        var bgPtnAdrHi = bgPtnAdrLo + 0x8;
        var bgPtn = (((read(bgPtnAdrHi) & (1 << bgPX)) << 1) | (read(bgPtnAdrLo) & (1 << bgPX))) >> bgPX;
        if (bgPtn == 0) return false;

        var spPtnAdrLo = 0;
        if (spriteSize == 0)
        {
            spPtnAdrLo = spritePatternTable + (oam[1] << 0x4);
        }
        else
        {
            spPtnAdrLo = 0x1000 * (oam[1] & 1) + (oam[1] >> 1);
        }
        var hFlip = oam[2] & 0x40;
        var vFlip = oam[2] & 0x80;
        var oX = hFlip == 0 ? 7 - (_x - sp0_X) : _x - sp0_X;
        var oY = vFlip == 0 ? _y - sp0_Y : 7 - (_y - sp0_Y);
        spPtnAdrLo += oY;
        var spPtnAdrHi = spPtnAdrLo + 0x8;
        var spPtn = ((read(spPtnAdrHi) & (1 << oX)) << 1) | (read(spPtnAdrLo) & (1 << oX)) >> oX;
        if (spPtn == 0 || sprite0Hit == 1)
        {
            return false;
        }
        return true;
    }
    private int ___(int value) => 0;

    public int _2000(int value)
    {
        baseNameTable = 0x2000 + ((value & 3) * 0x400);
        increment = (value >> 2) & 1;
        spritePatternTable = ((value >> 3) & 1) * 0x1000;
        bgPatternTable = ((value >> 4) & 1) * 0x1000;
        spriteSize = (value >> 5) & 1;
        masterSlave = (value >> 6) & 1;
        nmiEnable = ((value >> 7) & 1) & 1;
        w = 0;
        return t = (t & 0xf3ff) | ((value & 0x03) << 10);
    }

    public int _2001(int value)
    {
        grayscale = (value >> 0) & 1;
        showLeftBackground = (value >> 1) & 1;
        showLeftSprites = (value >> 2) & 1;
        showBackground = (value >> 3) & 1;
        showSprites = (value >> 4) & 1;
        redTint = (value >> 5) & 1;
        greenTint = (value >> 6) & 1;
        return blueTint = (value >> 7) & 1;
    }

    public int _2002(int value)
    {
        var res = w = 0;//register & 0x1f;
        res |= spriteOverflow << 5;
        res |= sprite0Hit << 6;
        res |= vBlank << 7;
        vBlank = 0;
        return res;
    }

    public int _2003(int value)
    {
        oamAdr = value & 0xff;
        return 0;
    }

    public int _2004(int value)
    {
        oam[oamAdr] = value;
        return 0;
    }

    public int _2005(int value)
    {
        if (w == 0)
        {
            w = 1;
            t = (t & 0xffe0) | (value >> 3);
            x = value & 0x07;
            scrollX = value;
        }
        else
        {
            w = 0;
            t = (t & 0x8fff) | ((value & 0x07) << 12);
            t = (t & 0xfc1f) | ((value & 0xf8) << 2);
            scrollY = value;
        }

        return 0;
    }

    public int _2006(int value)
    {
        if (w == 0)
        {
            w = 1;
            t = (t & 0x80ff) | ((value & 0x3f) << 8);

            tmp = (value & 0x3f) << 8;

            baseNameTable = 0x2000 + (((value & 0b00001100) >> 2) * 0x400);
        }
        else
        {
            w = 0;
            t = (t & 0xFF00) | value;
            v = t;

            adr = tmp | value;
        }
        return 0;
    }

    private long w2007cnt;
    public int _2007(int value)
    {
        if (value == int.MaxValue)
        {
            var data = buf;
            buf = read(adr);

            if (adr >= 0x3f00)
            {
                data = buf;
            }
            adr += increment == 0 ? 1 : 32;
            v += increment == 0 ? 1 : 32;
            return data;
        }
        else
        {
            w2007cnt++;

            write(adr, value);
            //if (w2007cnt < 10000)
            //    File.AppendAllLines(logPath, new[] { $"cnt:{w2007cnt.ToString("D8")}, adr:{adr.ToString("X4")}, value:{value.ToString("X4")}" });
            adr += increment == 0 ? 1 : 32;
            return 0;
        }
    }

    //private void updateNameTableTex()
    //{
    //    for (var i = 0; i < 4; i++)
    //    {
    //        var ntAdr = 0x2000 + (0x400 * i);

    //        var bgPalette = new int[16];
    //        for (var pi = 0; pi < 16; pi++)
    //        {
    //            var _i = pi == 0 || pi == 4 || pi == 8 || pi == 0xC ? 0 : pi;
    //            bgPalette[pi] = read(0x3f00 + _i);
    //        }

    //        var ox = (i % 2) * 256;
    //        var oy = (i >> 1) * 240;

    //        for (var y = 0; y < 30; y++)
    //        {
    //            var pyB = y >> 2;
    //            var pyO = (y >> 1) & 1;
    //            for (var x = 0; x < 32; x++)
    //            {
    //                var ptnNo = read(ntAdr + (y * 32) + x);

    //                var pxB = x >> 2;
    //                var pB = (pyB << 3) + pxB;
    //                var palByte = read(ntAdr + 0x3c0 + pB);
    //                var pxO = (x >> 1) & 1;
    //                var offset = (pyO << 1) + pxO;
    //                var pal = (palByte >> (offset << 1)) & 0x3;

    //                for (var r = 0; r < 8; r++)
    //                {
    //                    var ptnTblAdr = bgPatternTable + (ptnNo * 16) + r;
    //                    for (var c = 0; c < 8; c++)
    //                    {
    //                        var ptnLo = (read(ptnTblAdr) >> c) & 1;
    //                        var ptnHi = (read(ptnTblAdr + 8) >> c) & 1;
    //                        var ptn = (ptnHi << 1) + ptnLo;
    //                        var col = palette[bgPalette[(pal << 2) + ptn]];
    //                        var idx = (479 - ((y * 8) + r + oy)) * 512 + ((x * 8) + (7 - c) + ox);
    //                        nameTablePix[idx + 0] = col.R8;
    //                    }
    //                }
    //            }
    //        }
    //        nameTableTex.SetPixels32(nameTablePix);
    //        nameTableTex.Apply();
    //    }
    //}

    private void createPalette()
    {
        for (var i = 0; i < 64; i++)
        {
            palette[i] = new Color(nesPalette[i]);
        }
    }

    //private void updatePalleteTex()
    //{
    //    for (var i = 0; i < 64; i++)
    //    {
    //        var p = palette[i];
    //        palettePix[i] = p.R8;
    //    }
    //    paletteTex.SetPixels32(palettePix);
    //    paletteTex.Apply();
    //}

    private void updatePatternTableTex()
    {
        for (var t = 0; t < 2; t++)
        {
            for (var y = 0; y < 16; y++)
            {
                for (var x = 0; x < 16; x++)
                {
                    for (var r = 0; r < 8; r++)
                    {
                        var idx = (t * (16 * 16 * (8 * 2))) + (y * 16 * 8 * 2) + (x * 8 * 2) + r;
                        for (var c = 0; c < 8; c++)
                        {
                            var lo = (read(idx) >> c) & 1;
                            var hi = (read(idx + 8) >> c) & 1;
                            var val = (hi << 1) + lo;
                            var col = cols[val];
                            var idx2 = ((y * 8 + r) * (8 * 16 * 2 * 4)) + ((x * 8 * 4) + ((7 - c) * 4) + (t * 8 * 16 * 4));
                            patternPix[idx2 + 0] = (byte)col.R8;
                            patternPix[idx2 + 1] = (byte)col.G8;
                            patternPix[idx2 + 2] = (byte)col.B8;
                            patternPix[idx2 + 3] = (byte)col.A8;
                        }
                    }
                }
            }
        }
        patternImg.SetData(256, 128, false, Image.Format.Rgba8, patternPix);
        patternTex.SetImage(patternImg);
    }
}

//public struct PPURegister
//{
//    public ushort data;

//    public byte CoarseX
//    {
//        get => (byte)(data & 0x1F);
//        set => data = (ushort)(data & ~0x1F | value & 0x1F);
//    }
//    public byte CoarseY
//    {
//        get => (byte)(data >> 5 & 0x1F);
//        set => data = (ushort)(data & ~(0x1F << 5) | (value & 0x1F) << 5);
//    }
//    public byte NametableX
//    {
//        get => (byte)(data >> 10 & 0x01);
//        set => data = (ushort)(data & ~(0x01 << 10) | (value & 0x01) << 10);
//    }
//    public byte NametableY
//    {
//        get => (byte)(data >> 11 & 0x01);
//        set => data = (ushort)(data & ~(0x01 << 11) | (value & 0x01) << 11);
//    }
//    public byte FineY
//    {
//        get => (byte)(data >> 12 & 0x07);
//        set => data = (ushort)(data & ~(0x07 << 12) | (value & 0x07) << 12);
//    }

//    public byte AddrHi
//    {
//        set => data = (ushort)(data & ~(0x3F << 8) | (value & 0x3F) << 8);
//    }
//    public byte AddrLo
//    {
//        set => data = (ushort)(data & ~0x00FF | value);
//    }

//    public PPURegister(ushort newData)
//    {
//        data = newData;
//    }

//    public static PPURegister operator +(PPURegister r, int i) => new((ushort)(r.data + i));
//}