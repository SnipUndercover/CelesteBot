﻿using Microsoft.Xna.Framework;
using System;

namespace CelesteBot_2023
{
    public class InputData
    {
        public static int LongJumpFrameCount = 20;
        public static int LongJumpRemainingTimer = 0;
        public static double LongJumpPersistentValue = 0;

        public float MoveX;
        public float MoveY;
        public Vector2 Aim;
        public Vector2 MountainAim;
        public int Buttons;

        public double JumpValue;
        public double DashValue;
        public double GrabValue;
        public double LongJumpValue;


        ///<summary>
        ///This constructor uses the actions float array to create virtual inputs from the actions.
        ///Refrenced from CelesteBotPlayer
        ///</summary>
        ///<param name="actions"actions array with length 6.</param>
        public InputData(Action action)
        {

            UpdateData(action);
            //this.ESC = actions[2] > CelesteBotManager.ACTION_THRESHOLD;
            //this.MenuConfirm = actions[3] > CelesteBotManager.ACTION_THRESHOLD;
            //this.MenuCancel = actions[4] > CelesteBotManager.ACTION_THRESHOLD;
            //this.QuickRestart = actions[5] > CelesteBotManager.ACTION_THRESHOLD;
            //LongJumpValue = actions[5];
            //bool LongJump = Math.Abs(actions[5]) > CelesteBotManager.ACTION_THRESHOLD;

            //    LastIndex = CelesteBotInteropModule.CurrentPlayer.GetHashCode();

            //}
            if (LongJumpRemainingTimer > 0)
            {
                Jump = true;
                LongJumpRemainingTimer--;
                //LongJumpValue = LongJumpPersistentValue;
                //JumpValue = LongJumpPersistentValue;
            }
            
        }

        public InputData()
        {
            if (LongJumpRemainingTimer > 0)
            {
                Jump = true;
                LongJumpRemainingTimer--;
                //LongJumpValue = LongJumpPersistentValue;
                //JumpValue = LongJumpPersistentValue;
            }
        }


        public void UpdateData(Action action)
        {
            //when using a keyboard, Input.MoveX.Value is -1 when pressing left, 1 when pressing right, 0 otherwise. (same applies for Input.MoveY.Value)
            MoveX = action.GetMoveX();
            MoveY = action.GetMoveY();

            // + or - actions allow for buttons
            Jump = action.GetJump();
            bool LongJump = action.GetLongJump();
            Dash = action.GetDash();
            Grab = action.GetGrab();
            MenuConfirm = action.GetMenuConfirm();
            MenuDown = action.GetMenuDown();
            Pause = action.GetPause();
            if (LongJumpRemainingTimer > 0)
            {
                Jump = true;
                LongJumpRemainingTimer--;
                //LongJumpValue = LongJumpPersistentValue;
                //JumpValue = LongJumpPersistentValue;
            }
            else if (LongJump && LongJumpRemainingTimer == 0)
            {
                Jump = true;
                LongJumpRemainingTimer = LongJumpFrameCount;
                //LongJumpPersistentValue = LongJumpValue;
                //JumpValue = LongJumpValue;
            }
            if (Dash)
            {
                Aim = new Vector2(MoveX, MoveY);
            }
        }
        public bool ESC
        {
            get
            {
                return (Buttons & (int)ButtonMask.ESC) == (int)ButtonMask.ESC;
            }
            set
            {
                Buttons &= (int)~ButtonMask.ESC;
                if (value)
                    Buttons |= (int)ButtonMask.ESC;
            }
        }
        public bool Pause
        {
            get
            {
                return (Buttons & (int)ButtonMask.Pause) == (int)ButtonMask.Pause;
            }
            set
            {
                Buttons &= (int)~ButtonMask.Pause;
                if (value)
                    Buttons |= (int)ButtonMask.Pause;
            }
        }
        public bool MenuConfirm
        {
            get
            {
                return (Buttons & (int)ButtonMask.MenuConfirm) == (int)ButtonMask.MenuConfirm;
            }
            set
            {
                Buttons &= (int)~ButtonMask.MenuConfirm;
                if (value)
                    Buttons |= (int)ButtonMask.MenuConfirm;
            }
        }
        public bool MenuCancel
        {
            get
            {
                return (Buttons & (int)ButtonMask.MenuCancel) == (int)ButtonMask.MenuCancel;
            }
            set
            {
                Buttons &= (int)~ButtonMask.MenuCancel;
                if (value)
                    Buttons |= (int)ButtonMask.MenuCancel;
            }
        }
        public bool MenuDown
        {
            get
            {
                return (Buttons & (int)ButtonMask.MenuDown) == (int)ButtonMask.MenuDown;
            }
            set
            {
                Buttons &= (int)~ButtonMask.MenuDown;
                if (value)
                    Buttons |= (int)ButtonMask.MenuDown;
            }
        }
        public bool QuickRestart
        {
            get
            {
                return (Buttons & (int)ButtonMask.QuickRestart) == (int)ButtonMask.QuickRestart;
            }
            set
            {
                Buttons &= (int)~ButtonMask.QuickRestart;
                if (value)
                    Buttons |= (int)ButtonMask.QuickRestart;
            }
        }
        public bool Jump
        {
            get
            {
                return (Buttons & (int)ButtonMask.Jump) == (int)ButtonMask.Jump;
            }
            set
            {
                Buttons &= (int)~ButtonMask.Jump;
                if (value)
                    Buttons |= (int)ButtonMask.Jump;
            }
        }


        public bool Dash
        {
            get
            {
                return (Buttons & (int)ButtonMask.Dash) == (int)ButtonMask.Dash;
            }
            set
            {
                Buttons &= (int)~ButtonMask.Dash;
                if (value)
                    Buttons |= (int)ButtonMask.Dash;
            }
        }
        public bool Grab
        {
            get
            {
                return (Buttons & (int)ButtonMask.Grab) == (int)ButtonMask.Grab;
            }
            set
            {
                Buttons &= (int)~ButtonMask.Grab;
                if (value)
                    Buttons |= (int)ButtonMask.Grab;
            }
        }
        public bool Talk
        {
            get
            {
                return (Buttons & (int)ButtonMask.Talk) == (int)ButtonMask.Talk;
            }
            set
            {
                Buttons &= (int)~ButtonMask.Talk;
                if (value)
                    Buttons |= (int)ButtonMask.Talk;
            }
        }
        public override String ToString()
        {
            string outp = "InputData: (x,y): (" + MoveX + ", " + MoveY + ") + buttons: (";
            string[] names = Enum.GetNames(typeof(ButtonMask));
            Array values = Enum.GetValues(typeof(ButtonMask));
            for (int i = 0; i < names.Length; i++)
            {
                if ((Buttons & (int)values.GetValue(i)) == (int)values.GetValue(i))
                {
                    outp += names[i] + ", ";
                }
            }
            outp += ")";
            return outp;
        }



        [Flags]
        public enum ButtonMask : int
        {
            ESC = 1 << 0,
            QuickRestart = 1 << 2,
            MenuConfirm = 1 << 3,
            MenuCancel = 1 << 4,
            MenuDown = 1 << 5,
            Jump = 1 << 6,
            Dash = 1 << 7,
            Grab = 1 << 8,
            Talk = 1 << 9, // shouldn't really be needed
            Pause = 1 << 10
        }
    }
}
