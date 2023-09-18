using Godot;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
//File.AppendAllLines(logPath, new[] { $"sc:{stepCnt.ToString("D8")}, pc:{pc.ToString("X4")}, cy:{cycles}, op:{opcode.ToString("X2")}, a:{a.ToString("X2")}, x:{x.ToString("X2")}, y:{y.ToString("X2")}, c:{(p.HasFlag(Status.Carry) ? 1 : 0)}, z:{(p.HasFlag(Status.Zero) ? 1 : 0)}, i:{(p.HasFlag(Status.InterruptDisabled) ? 1 : 0)}, d:{(p.HasFlag(Status.DecimalMode) ? 1 : 0)}, b4:{(p.HasFlag(Status.Bit4) ? 1 : 0)}, b5:{(p.HasFlag(Status.Bit5) ? 1 : 0)}, v:{(p.HasFlag(Status.Overflow) ? 1 : 0)}, n:{(p.HasFlag(Status.Negative) ? 1 : 0)}" });

public class CPU
{
    public ROM rom;
    public PPU ppu;
    public NesController joypad1, joypad2;
    //public APU apu;
    //public NesController joypad1, joypad2;

    public const int _a = 0x10000;
    public const int _x = 0x10001;
    public const int _y = 0x10002;
    public const int _s = 0x10003;
    public const int _p = 0x10004;

    public int a { get { return mem[_a]; } set { mem[_a] = value; } }
    public int x { get { return mem[_x]; } set { mem[_x] = value; } }
    public int y { get { return mem[_y]; } set { mem[_y] = value; } }
    public int s { get { return mem[_s]; } set { mem[_s] = value; } }
    public int p
    {
        get
        {
            return (c << 0) |
                (z << 1) |
                (i << 2) |
                (d << 3) |
                (bit4 << 4) |
                (bit5 << 5) |
                (v << 6) |
                (n << 7);
        }
        set
        {
            c = (value >> 0) & 1;
            z = (value >> 1) & 1;
            i = (value >> 2) & 1;
            d = (value >> 3) & 1;
            v = (value >> 6) & 1;
            n = (value >> 7) & 1;
        }
    }

    public int pc;

    public int c;
    public int z;
    public int i;
    public int d;
    public int bit4 = 1;
    public int bit5 = 1;
    public int n;
    public int v;

    public Action[] opcodes;
    public Action[] addressingModes;
    public int[] opcodeSize;
    public int[] opcodeCycles;
    public int[] mem;

    public const int NMI_VECTOR = 0xFFFA;
    public const int RESET_VECTOR = 0xFFFC;
    public const int IRQBRK_VECTOR = 0xFFFE;

    public int adr;
    public Stack stack = new Stack();
    public long cycles;

    public int _ = int.MaxValue;

    public int isIRQ = 0;
    public int isNMI = 0;

    public class StepRes
    {
        public long stepCnt;
        public int adr;
        public int pc;
        public int cy;
        public int opcode;
        public int a;
        public int x;
        public int y;
        public int c;
        public int z;
        public int i;
        public int d;
        public int b4;
        public int b5;
        public int v;
        public int n;
    }
    public CPU(PPU _ppu, NesController _joypad1, NesController _joypad2)
    {
        //this.logPath = logPath;
        ppu = _ppu;
        joypad1 = _joypad1;
        joypad2 = _joypad2;
        rom = ppu.rom;

        logPath = "cpu2.log";
        if (File.Exists(logPath))
        {
            File.Delete(logPath);
        }

        HardReset();


        ////ppu.OnEnable();
        ppu.OnNmi += () =>
        {
            isNMI = 1;
        };
        opcodes = new Action[]
        {
            //        0    1    2    3    4    5    6    7    8    9    A    B    C    D    E    F
            /* 00 */ BRK, ORA, ___, ___, ___, ORA, ASL, ___, PHP, ORA, ASL, ___, ___, ORA, ASL, ___,
            /* 10 */ BPL, ORA, ___, ___, ___, ORA, ASL, ___, CLC, ORA, ___, ___, ___, ORA, ASL, ___,
            /* 20 */ JSR, AND, ___, ___, BIT, AND, ROL, ___, PLP, AND, ROL, ___, BIT, AND, ROL, ___,
            /* 30 */ BMI, AND, ___, ___, ___, AND, ROL, ___, SEC, AND, ___, ___, ___, AND, ROL, ___,
            /* 40 */ RTI, EOR, ___, ___, ___, EOR, LSR, ___, PHA, EOR, LSR, ___, JMP, EOR, LSR, ___,
            /* 50 */ BVC, EOR, ___, ___, ___, EOR, LSR, ___, CLI, EOR, ___, ___, ___, EOR, LSR, ___,
            /* 60 */ RTS, ADC, ___, ___, ___, ADC, ROR, ___, PLA, ADC, ROR, ___, JMP, ADC, ROR, ___,
            /* 70 */ BVS, ADC, ___, ___, ___, ADC, ROR, ___, SEI, ADC, ___, ___, ___, ADC, ROR, ___,
            /* 80 */ ___, STA, ___, ___, STY, STA, STX, ___, DEY, ___, TXA, ___, STY, STA, STX, ___,
            /* 90 */ BCC, STA, ___, ___, STY, STA, STX, ___, TYA, STA, TXS, ___, ___, STA, ___, ___,
            /* A0 */ LDY, LDA, LDX, ___, LDY, LDA, LDX, ___, TAY, LDA, TAX, ___, LDY, LDA, LDX, ___,
            /* B0 */ BCS, LDA, ___, ___, LDY, LDA, LDX, ___, CLV, LDA, TSX, ___, LDY, LDA, LDX, ___,
            /* C0 */ CPY, CMP, ___, ___, CPY, CMP, DEC, ___, INY, CMP, DEX, ___, CPY, CMP, DEC, ___,
            /* D0 */ BNE, CMP, ___, ___, ___, CMP, DEC, ___, CLD, CMP, ___, ___, ___, CMP, DEC, ___,
            /* E0 */ CPX, SBC, ___, ___, CPX, SBC, INC, ___, INX, SBC, NOP, ___, CPX, SBC, INC, ___,
            /* F0 */ BEQ, SBC, ___, ___, ___, SBC, INC, ___, SED, SBC, ___, ___, ___, SBC, INC, ___,
        };

        addressingModes = new Action[]
        {
            //        0    1    2    3    4    5    6    7    8    9    A    B    C    D    E    F
            /* 00 */ IMP, IDX, ___, ___, ___, ZPG, ZPG, ___, IMP, IMD, ACM, ___, ___, ABS, ABS, ___,
            /* 10 */ REL, IDY, ___, ___, ___, ZPX, ZPX, ___, IMP, ABY, ___, ___, ___, ABX, ABX, ___,
            /* 20 */ ABS, IDX, ___, ___, ZPG, ZPG, ZPG, ___, IMP, IMD, ACM, ___, ABS, ABS, ABS, ___,
            /* 30 */ REL, IDY, ___, ___, ___, ZPX, ZPX, ___, IMP, ABY, ___, ___, ___, ABX, ABX, ___,
            /* 40 */ IMP, IDX, ___, ___, ___, ZPG, ZPG, ___, IMP, IMD, ACM, ___, ABS, ABS, ABS, ___,
            /* 50 */ REL, IDY, ___, ___, ___, ZPX, ZPX, ___, IMP, ABY, ___, ___, ___, ABX, ABX, ___,
            /* 60 */ IMP, IDX, ___, ___, ___, ZPG, ZPG, ___, IMP, IMD, ACM, ___, IND, ABS, ABS, ___,
            /* 70 */ REL, IDY, ___, ___, ___, ZPX, ZPX, ___, IMP, ABY, ___, ___, ___, ABX, ABX, ___,
            /* 80 */ ___, IDX, ___, ___, ZPG, ZPG, ZPG, ___, IMP, ___, IMP, ___, ABS, ABS, ABS, ___,
            /* 90 */ REL, IDY, ___, ___, ZPX, ZPX, ZPY, ___, IMP, ABY, IMP, ___, ___, ABX, ___, ___,
            /* A0 */ IMD, IDX, IMD, ___, ZPG, ZPG, ZPG, ___, IMP, IMD, IMP, ___, ABS, ABS, ABS, ___,
            /* B0 */ REL, IDY, ___, ___, ZPX, ZPX, ZPY, ___, IMP, ABY, IMP, ___, ABX, ABX, ABY, ___,
            /* C0 */ IMD, IDX, ___, ___, ZPG, ZPG, ZPG, ___, IMP, IMD, IMP, ___, ABS, ABS, ABS, ___,
            /* D0 */ REL, IDY, ___, ___, ___, ZPX, ZPX, ___, IMP, ABY, ___, ___, ___, ABX, ABX, ___,
            /* E0 */ IMD, IDX, ___, ___, ZPG, ZPG, ZPG, ___, IMP, IMD, IMP, ___, ABS, ABS, ABS, ___,
            /* F0 */ REL, IDY, ___, ___, ___, ZPX, ZPX, ___, IMP, ABY, ___, ___, ___, ABX, ABX, ___,
        };

        var _ = 0;

        opcodeSize = new int[]
        {
            //       0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F
            /* 00 */ 1, 2, _, _, _, 2, 2, _, 1, 2, 1, _, _, 3, 3, _,
            /* 10 */ 2, 2, _, _, _, 2, 2, _, 1, 3, _, _, _, 3, 3, _,
            /* 20 */ 3, 2, _, _, 2, 2, 2, _, 1, 2, 1, _, 3, 3, 3, _,
            /* 30 */ 2, 2, _, _, _, 2, 2, _, 1, 3, _, _, _, 3, 3, _,
            /* 40 */ 1, 2, _, _, _, 2, 2, _, 1, 2, 1, _, 3, 3, 3, _,
            /* 50 */ 2, 2, _, _, _, 2, 2, _, 1, 3, _, _, _, 3, 3, _,
            /* 60 */ 1, 2, _, _, _, 2, 2, _, 1, 2, 1, _, 3, 3, 3, _,
            /* 70 */ 2, 2, _, _, _, 2, 2, _, 1, 3, _, _, _, 3, 3, _,
            /* 80 */ _, 2, _, _, 2, 2, 2, _, 1, _, 1, _, 3, 3, 3, _,
            /* 90 */ 2, 2, _, _, 2, 2, 2, _, 1, 3, 1, _, _, 3, _, _,
            /* A0 */ 2, 2, 2, _, 2, 2, 2, _, 1, 2, 1, _, 3, 3, 3, _,
            /* B0 */ 2, 2, _, _, 2, 2, 2, _, 1, 3, 1, _, 3, 3, 3, _,
            /* C0 */ 2, 2, _, _, 2, 2, 2, _, 1, 2, 1, _, 3, 3, 3, _,
            /* D0 */ 2, 2, _, _, _, 2, 2, _, 1, 3, _, _, _, 3, 3, _,
            /* E0 */ 2, 2, _, _, 2, 2, 2, _, 1, 2, 1, _, 3, 3, 3, _,
            /* F0 */ 2, 2, _, _, _, 2, 2, _, 1, 3, _, _, _, 3, 3, _
        };

        opcodeCycles = new int[] {
            //       0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F
            /* 00 */ 7, 6, _, _, _, 3, 5, _, 3, 2, 2, _, _, 4, 6, _,
            /* 10 */ 2, 5, _, _, _, 4, 6, _, 2, 4, _, _, _, 4, 7, _,
            /* 20 */ 6, 6, _, _, 3, 3, 5, _, 4, 2, 2, _, 4, 4, 6, _,
            /* 30 */ 2, 5, _, _, _, 4, 6, _, 2, 4, _, _, _, 4, 7, _,
            /* 40 */ 6, 6, _, _, _, 3, 5, _, 3, 2, 2, _, 3, 4, 6, _,
            /* 50 */ 2, 5, _, _, _, 4, 6, _, 2, 4, _, _, _, 4, 7, _,
            /* 60 */ 6, 6, _, _, _, 3, 5, _, 4, 2, 2, _, 5, 4, 6, _,
            /* 70 */ 2, 5, _, _, _, 4, 6, _, 2, 4, _, _, _, 4, 7, _,
            /* 80 */ _, 6, _, _, 3, 3, 3, _, 2, _, 2, _, 4, 4, 4, _,
            /* 90 */ 2, 6, _, _, 4, 4, 4, _, 2, 5, 2, _, _, 5, _, _,
            /* A0 */ 2, 6, 2, _, 3, 3, 3, _, 2, 2, 2, _, 4, 4, 2, _,
            /* B0 */ 2, 5, _, _, 4, 4, 4, _, 2, 4, 2, _, 4, 4, 4, _,
            /* C0 */ 2, 6, _, _, 3, 3, 5, _, 2, 2, 2, _, 4, 4, 6, _,
            /* D0 */ 2, 5, _, _, _, 4, 6, _, 2, 4, _, _, _, 4, 7, _,
            /* E0 */ 2, 6, _, _, 3, 3, 5, _, 2, 2, 2, _, 4, 4, 6, _,
            /* F0 */ 2, 5, _, _, _, 4, 6, _, 2, 4, _, _, _, 4, 7, _
        };

    }

    public void HardReset()
    {
        stack.Clear();
        cycles = 0;
        mem = new int[0x10005];
        p = 0x34;
        s = 0xFD;
        pc = mem16(RESET_VECTOR);
    }

    public void SoftReset()
    {
        cycles = 0;
        i = 1;
        push16(pc);
        push8(p);
        pc = mem16(RESET_VECTOR);
    }

    public void IRQ()
    {
        pc = mem16(NMI_VECTOR);
    }

    private long stepCnt = 0;

    public void RaiseNmi() => isNMI = 1;

    private string logPath;

    public long Step(int logPoint = 5000)
    {
        stepCnt++;

        var prevCycles = cycles;
        //if (isIRQ == 1) IRQ();
        //isIRQ = 0;

        if (isNMI == 1)
        {
            isNMI = 0;
            push16(pc);
            push8(p | 0x20);
            pc = mem16(NMI_VECTOR);
            cycles += 7;
            return cycles - prevCycles;
        }

        //Debug.Log($"{pc.ToString("X4")}");
        if (pc == 0x8eb9)
        {

        }
        var opcode = this[pc];


        var op = opcodes[opcode];
        pc++;
        addressingModes[opcode]();
        //if (stepCnt > 0x3cf00 && stepCnt < 0x3d000)
        //    Debug.Log($"{stepCnt.ToString("D8")}, adr:{adr.ToString("X4")}, op:{opcodes[opcode].Method.Name}, a:{a.ToString("X2")}, x:{x.ToString("X2")}, y:{y.ToString("X2")}");
        op();
        cycles += opcodeCycles[opcode];

        return cycles - prevCycles;
    }

    private static ushort GetPPUAddress(ushort address)
    {
        return (ushort)((address - 0x2000) % 8 + 0x2000);
    }

    public int this[int adr]
    {
        get
        {
            //if (adr == 0x4016)
            //{
            //    Debug.Log(joypad1.buttonState);
            //}
            return adr switch
            {
                < 0x2000 => mem[adr],
                < 0x4000 => ppu.bus[adr & 0x7](_), // ppu.ReadRegister(GetPPUAddress((ushort)adr)), // 
                //< 0x4015 => apu.bus[adr](_),
                0x4016 => joypad1.buttonState,
                //0x4017 => joypad2.buttonState,
                < 0x8000 => 0, // Battery backup RAM
                < 0x10000 => rom.read(adr - 0x8000, false),
                < 0x10005 => mem[adr],
                _ => 0
            };
        }

        set
        {
            if (adr == 0x4014)
            {
                adr = value << 8;
                var oamAdr = 0;
                for (var i = 0; i < 0xff; i++)
                {
                    ppu.oam[oamAdr] = (byte)this[adr + oamAdr];
                    oamAdr++;
                }

                cycles += 512;
                return;
            }

            switch (adr)
            {
                case < 0x2000:
                    mem[adr] = value;
                    break;
                case < 0x4000: ppu.bus[adr & 0x7](value); break;
                case 0x4016: joypad1.buttonState = value; break;
                //case 0x4017: joypad2.buttonState = value; break;
                case >= 0x10000: mem[adr] = value; break;
            }
        }
    }

    public bool cross(int prevAdr, int adr)
    {
        return (prevAdr >> 8) != (prevAdr >> 8);
    }

    public void push8(int value)
    {
        stack.Push(value);
        s -= 1;
    }

    public void push16(int value)
    {
        stack.Push(value >> 8);
        stack.Push(value & 0xff);
        s -= 2;
    }

    public int pull8()
    {
        s++;
        return (int)stack.Pop() & 0xff;
    }

    public int pull16()
    {
        s += 2;
        return pull8() | (pull8() << 8);
    }

    public int mem8(int adr)
    {
        return this[adr];
    }

    public int mem16(int adr)
    {
        return (mem8(adr + 1) << 8) | mem8(adr);
    }

    public void Start()
    {
    }

    public void IMP()
    {
        adr = 0;
    }

    public void ACM()
    {
        adr = _a;
    }

    public void IMD()
    {
        adr = pc++;
    }

    public void ZPG()
    {
        adr = this[pc++];
    }

    public void ZPX()
    {
        adr = this[pc++] + x;

    }

    public void ZPY()
    {
        adr = this[pc++] + y;
    }

    public void REL()
    {
        sbyte offset = (sbyte)mem8(pc);
        adr = pc + offset;
    }

    public void ABS()
    {
        adr = this[pc++] + (this[pc++] << 8);
    }

    public void ABX()
    {
        adr = ((this[pc++] + (this[pc++] << 8)) + x) & 0xffff;
        if (!cross(adr - x, adr)) return;
        //b = 1;
    }

    public void ABY()
    {
        adr = ((this[pc++] + (this[pc++] << 8)) + y) & 0xffff;
        if (!cross(adr - y, adr)) return;
        //b = 1;
    }

    public void IND()
    {
        adr = indirect(this[pc++] + (this[pc++] << 8));
    }

    public void IDX()
    {
        adr = indirect((this[pc++] + x) & 0xff);
    }

    public void IDY()
    {
        adr = (indirect(this[pc++]) + y) & 0xffff;
    }

    public int indirect(int low)
    {
        var hi = (low & 0xff00) | (((low & 0xff) + 1) & 0xff);
        low = this[low];
        hi = this[hi];

        return (hi << 8) | low;
    }

    //------------------------------------------------------------------------

    public void nz(int value)
    {
        z = value == 0 ? 1 : 0;
        n = (value & 0x80) != 0 ? 1 : 0;
    }

    public void LDA()
    {
        if (adr == 0x8ccb)
        {

        }
        a = this[adr];
        nz(a);
    }

    public void LDX()
    {
        x = this[adr];
        nz(x);
    }

    public void LDY()
    {
        y = this[adr];
        nz(y);
    }

    public void STA()
    {
        this[adr] = a;
    }

    public void STX()
    {
        this[adr] = x;
        if (adr != 0x4016)
        {

        }
    }

    public void STY()
    {
        this[adr] = y;
    }

    public void TAX()
    {
        x = a;
        nz(x);
    }

    public void TAY()
    {
        y = a;
        nz(y);
    }

    public void TSX()
    {
        x = s;
        nz(x);
    }

    public void TXA()
    {
        a = x;
        nz(a);
    }

    public void TXS()
    {
        s = x;
    }

    public void TYA()
    {
        a = y;
    }

    public void ADC()
    {
        var __a = a;
        a = a + this[adr] + c;
        c = a > 0xff ? 1 : 0;
        a &= 0xff;
        v = ((__a ^ a) & (this[adr] ^ a) & 0x80) == 0 ? 0 : 1;
        nz(a);
    }

    public void AND()
    {
        a &= this[adr];
        nz(a);
    }

    public void ASL()
    {
        var value = this[adr];
        c = (value >> 7) & 1;
        value = (value << 1) & 0xff;
        this[adr] = value;
        nz(value);
    }

    public void BIT()
    {
        var value = this[adr];
        v = (value >> 6) & 1;
        nz(value);
        z = (a & value) == 0 ? 1 : 0;
    }

    public void CMP()
    {
        var value = this[adr];
        var tmp = a - value;
        c = a >= value ? 1 : 0;
        nz(tmp);
    }

    public void CPX()
    {
        var value = this[adr];
        var tmp = x - value;
        c = x >= value ? 1 : 0;
        nz(tmp);
    }

    public void CPY()
    {
        var value = this[adr];
        var tmp = y - value;
        c = y >= value ? 1 : 0;
        nz(tmp);
    }

    public void DEC()
    {
        this[adr] = (this[adr] - 1) & 0xff;
        nz(this[adr]);
    }

    public void DEX()
    {
        x = (x - 1) & 0xff;
        nz(x);
    }

    public void DEY()
    {
        y = (y - 1) & 0xff;
        nz(y);
    }

    public void EOR()
    {
        a = (a ^ this[adr]) & 0xff;
        nz(a);
    }

    public void INC()
    {
        this[adr] = (this[adr] + 1) & 0xff;
        nz(this[adr]);
    }

    public void INX()
    {
        x = (x + 1) & 0xff;
        nz(x);
    }

    public void INY()
    {
        y = (y + 1) & 0xff;
        nz(y);
    }

    public void LSR()
    {
        c = this[adr] & 1;
        this[adr] >>= 1;
        nz(this[adr]);
    }

    public void ORA()
    {
        a |= this[adr];
        nz(a);
    }

    public void ROL()
    {
        var __c = c;
        c = (this[adr] >> 7) & 1;
        this[adr] = ((this[adr] << 1) & 0xff) | __c;
        nz(this[adr]);
        z = a == 0 ? 1 : 0;
    }

    public void ROR()
    {
        var __c = c;
        c = this[adr] & 1;
        this[adr] = (this[adr] >> 1) + (__c << 7);
        nz(this[adr]);
        z = a == 0 ? 1 : 0;
    }

    public void SBC()
    {
        var src1 = a;
        var src2 = this[adr];
        c = c == 1 ? 0 : 1;
        var result = (src1 - src2 - c) & 0xff;
        a = result;
        nz(result);
        c = src1 >= src2 + c ? 1 : 0;
        v = ((src1 ^ result) & 0x80) != 0 && ((src1 ^ src2) & 0x80) != 0 ? 1 : 0;
    }

    public void PHA()
    {
        push8(a);
    }

    public void PHP()
    {
        push8(p | 0x10);
    }

    public void PLA()
    {
        a = (int)pull8();
        nz(a);
    }

    public void PLP()
    {
        p = ((int)pull8() & 0xef) | 0x20;
    }

    public void JMP()
    {
        pc = adr & 0xffff;
    }

    public void JSR()
    {
        push16(pc - 1); // ---
        pc = adr & 0xffff;
    }

    public void RTS()
    {
        pc = (int)pull16() + 1;
    }

    public void RTI()
    {
        var _sv = pull8();
        p = (_sv & ~(0x10 | 0x20)) | (p & (0x10 | 0x20));
        pc = pull16();
    }

    public void BCC()
    {
        if (c != 0)
        {
            pc++;
            return;
        }
        cycles += cross(pc, adr) ? 2 : 1;
        pc = (adr + 1) & 0xffff;
    }

    public void BCS()
    {
        if (c == 0)
        {
            pc++;
            return;
        }
        cycles += cross(pc, adr) ? 2 : 1;
        pc = (adr + 1) & 0xffff;
    }

    public void BEQ()
    {
        if (z == 0)
        {
            pc++;
            return;
        }
        cycles += cross(pc, adr) ? 2 : 1;
        pc = (adr + 1) & 0xffff;
    }

    public void BMI()
    {
        if (n == 0)
        {
            pc++;
            return;
        }
        cycles += cross(pc, adr) ? 2 : 1;
        pc = (adr + 1) & 0xffff;
    }

    public void BNE()
    {
        if (z != 0)
        {
            pc++;
            return;
        }
        cycles += cross(pc, adr) ? 2 : 1;
        pc = (adr + 1) & 0xffff;
    }

    public void BPL()
    {
        if (n != 0)
        {
            pc++;
            return;
        }
        cycles += cross(pc, adr) ? 2 : 1;
        pc = (adr + 1) & 0xffff;
    }

    public void BVC()
    {
        if (v != 0)
        {
            pc++;
            return;
        }
        cycles = cross(pc, adr) ? 2 : 1;
        pc = (adr + 1) & 0xffff;
    }

    public void BVS()
    {
        if (v != 1)
        {
            pc++;
            return;
        }
        cycles = cross(pc, adr) ? 2 : 1;
        pc = (adr + 1) & 0xffff;
    }

    public void CLC()
    {
        c = 0;
    }

    public void CLD()
    {
        d = 0;
    }

    public void CLI()
    {
        i = 0;
    }

    public void CLV()
    {
        v = 0;
    }

    public void SEC()
    {
        c = 1;
    }

    public void SED()
    {
        d = 1;
    }

    public void SEI()
    {
        i = 1;
    }

    public void BRK()
    {
        push16(pc + 1);
        push8(p | 0x20 | 0x10);
        pc = mem16(IRQBRK_VECTOR);
    }

    public void NOP()
    {

    }

    public void ___()
    {

    }

    public void _IRQ()
    {
        cycles += 7;
        push16(pc);
        push8(p);
        i = 1;
        //b = 0;
        pc = mem16(0xFFFE);
    }

    public void _RESET()
    {
        cycles = 0;
        p = 0x30;
        s = 0xfd;
        pc = 0;
        push16(pc);
        push8(p);
        pc = mem16(0xfffc);
    }

    public void _NMI()
    {
        cycles += 7;
        push16(16);
        push8(p);
        i = 1;
        //b = 0;
        pc = mem16(0xfffa);
    }
}
