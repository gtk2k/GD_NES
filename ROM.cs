using System.IO;
using System.Text;

public class ROM
{
    public byte[] programRom;
    public byte[] characterRom;
    public int mirrorType; // H = 0, V = 1, 4 = 4


    private const int PRG_BANK_SIZE = 0x4000;
    private const int CHR_BANK_SIZE = 0x2000;

    private int prgSize, chrSize;

    public ROM(string filePath)
    {
        var data = File.ReadAllBytes(filePath);
        if (Encoding.ASCII.GetString(data, 0, 3) != "NES")
        {
            //Debug.LogError("Not Nes Rom");
            return;
        }

        prgSize = data[4];
        chrSize = data[5];
        programRom = new byte[prgSize * PRG_BANK_SIZE];
        characterRom = new byte[chrSize * CHR_BANK_SIZE];
        for (var i = 0; i < programRom.Length; i++)
        {
            programRom[i] = data[16 + i];
        }
        for (var i = 0; i < characterRom.Length; i++)
        {
            characterRom[i] = data[16 + prgSize * PRG_BANK_SIZE + i];
        }
        //GameObject.Find("ChrPattern").GetComponent<Renderer>().material.mainTexture = patternTexture;
        mirrorType = ((data[6] >> 2) & 4);
        if (mirrorType == 0) mirrorType = data[6] & 1;
    }

    

    private void Start()
    {
        //Load(Path.Combine(Application.streamingAssetsPath, "smb.nes"));
    }

    public int this[int adr]
    {
        get
        {
            if (adr > 0xffff)
            {
                return (int)characterRom[adr & 0xffff];
            }
            else
            {
                if (prgSize == 1 && adr >= PRG_BANK_SIZE)
                {
                    return (int)programRom[adr - PRG_BANK_SIZE];
                }
                else
                {
                    return (int)programRom[adr];
                }
            }
        }

        set
        {
            // Bank 切り替え
        }
    }

    public int read(int adr, bool isChar)
    {
        if (isChar)
        {
            return (int)characterRom[adr];
        }
        else
        {
            if (prgSize == 1 && adr >= PRG_BANK_SIZE)
            {
                return (int)programRom[adr - PRG_BANK_SIZE];
            }
            else
            {
                return (int)programRom[adr];
            }
        }
    }
}