using Godot;
using System;

public class NesController
{
    private int data;
    private int isInit;
    private Action reset;

    private enum Button : int
    {
        A = 1 << 7,
        B = 1 << 6,
        Select = 1 << 5,
        Start = 1 << 4,
        Up = 1 << 3,
        Down = 1 << 2,
        Left = 1 << 1,
        Right = 1 << 0,
    }

    public int buttonState
    {
        get
        {
            var ret = ((data & 0x80) > 0) ? 1 : 0;
            if (isInit == 0)
            {
                data <<= 1;
                data &= 0xff;
            }
            return ret;
        }
        set
        {
            isInit = value & 1;
        }
    }

    public NesController(Action _reset)
    {
        reset = _reset;
    }

    public void getInput()
    {
        updateData("NES_A", (int)Button.A);
        updateData("NES_B", (int)Button.B);
        updateData("NES_SELECT", (int)Button.Select);
        updateData("NES_START", (int)Button.Start);
        updateData("NES_UP", (int)Button.Up);
        updateData("NES_DOWN", (int)Button.Down);
        updateData("NES_LEFT", (int)Button.Left);
        updateData("NES_RIGHT", (int)Button.Right);

        if (Input.IsActionPressed("NES_RESET"))
        {
            reset?.Invoke();
        }
    }

    public void updateData(string mapName, int button)
    {
        if (Input.IsActionPressed(mapName))
        {
            data |= button;
        }
        else
        {
            data &= ~button;
        }
    } 
}
//public class NesController
//{
//    public int buttonState
//    {
//        get
//        {
//            //var ret = ((data & 0x80) > 0) ? 1 : 0;
//            //if (isInit == 0)
//            //{
//            //    data <<= 1;
//            //    data &= 0xff;
//            //}
//            //return ret;
//            return 0;
//        }
//        set
//        {
//            //isInit = value & 1;
//        }
//    }
//}