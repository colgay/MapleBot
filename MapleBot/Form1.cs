using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;

namespace MapleBot
{
    public partial class Form1 : Form
    {
        private bool _working = false;
        private IntPtr _handle = IntPtr.Zero;
        private Bitmap _characterBmp = null;
        private Bitmap _characterBmp2 = null;
        private Bitmap _monsterBmp = null;
        private Bitmap _monkeyBmp = null;
        private Bitmap _ladderBmp = null;
        private Bitmap _climbBmp = null;
        private InputSimulator _input = null;
        private DirectInputKeyCode _arrowKey = DirectInputKeyCode.DIK_0;
        private bool _climbing = false;
        private int _timeToClimb = 0;
        private bool _finished = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _characterBmp = Win32.ConvertBitmap(new Bitmap("character.bmp"), PixelFormat.Format32bppArgb, true);
            _characterBmp2 = Win32.ConvertBitmap(new Bitmap("character2.bmp"), PixelFormat.Format32bppArgb, true);
            _monsterBmp = Win32.ConvertBitmap(new Bitmap("monster.bmp"), PixelFormat.Format32bppArgb, true);
            _monkeyBmp = Win32.ConvertBitmap(new Bitmap("monkey.bmp"), PixelFormat.Format32bppArgb, true);
            _ladderBmp = Win32.ConvertBitmap(new Bitmap("ladder.bmp"), PixelFormat.Format32bppArgb, true);
            _climbBmp = Win32.ConvertBitmap(new Bitmap("climb.bmp"), PixelFormat.Format32bppArgb, true);

            _input = new InputSimulator();

            HotKeyManager.RegisterHotKey(Keys.F2, KeyModifiers.Control);
            HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(HotKey_Pressed);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ToggleProgram();
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            timer2.Stop();

            if (CheckGameWindow(false))
            {
                timer2.Enabled = true;

                Rect rect = new Rect();
                Win32.GetWindowRect(_handle, ref rect);

                rect.Left += 3;
                rect.Top += 26; // 22 for classic windows theme
                rect.Right -= 3;
                rect.Bottom -= 3;

                using (Bitmap screen = Win32.CopyFromScreen(rect.Left + 218, rect.Top + 580, 217, 18,
                    PixelFormat.Format32bppArgb))
                {
                    if (screen.GetPixel(53, 0) == Color.FromArgb(255, 255, 0, 0)
                        || screen.GetPixel(53, 6) == Color.FromArgb(255, 190, 190, 190))
                    {
                        _input.Keyboard.KeyPress(DirectInputKeyCode.DIK_NEXT);
                    }

                    if (screen.GetPixel(160, 0) == Color.FromArgb(255, 255, 0, 0)
                        || screen.GetPixel(161, 6) == Color.FromArgb(255, 190, 190, 190))
                    {
                        _input.Keyboard.KeyPress(DirectInputKeyCode.DIK_END);
                    }
                }
            }
        }

        private async void timer3_Tick(object sender, EventArgs e)
        {
            timer3.Stop();

            if (CheckGameWindow(false))
            {
                timer3.Enabled = true;
                await UsePassiveSkills();
            }
        }

        private async void timer1_Tick(object sender, EventArgs e)
        {
            if (CheckGameWindow(false))
            {
                timer1.Stop();
                timer1.Enabled = true;

                _timeToClimb += 100;

                if (!_finished)
                    return;

                _finished = false;

                Stopwatch stopWatch = new Stopwatch();

                Rect rect = new Rect();
                Win32.GetWindowRect(_handle, ref rect);

                rect.Left += 3;
                rect.Top += 26; // 22 for classic windows theme
                rect.Right -= 3;
                rect.Bottom -= 3 + 68;

                using (Bitmap screen = Win32.CopyFromScreen(rect.Left, rect.Top, (rect.Right - rect.Left), 
                    (rect.Bottom - rect.Top), PixelFormat.Format32bppArgb))
                {
                    Point point = new Point();

                    if (Win32.FindImageColor(screen, _characterBmp, Color.FromArgb(255, 255, 255), 0, 0, screen.Width,
                        screen.Height, ref point))
                    {
                        Point characterPos = new Point(point.X + 26, point.Y - 7);

                        int posX = Math.Max(characterPos.X - 360, 0);
                        int posY = Math.Max(characterPos.Y - 90, 0);
                        int width = Math.Min(characterPos.X + 360, screen.Width) - posX;
                        int height = Math.Min(characterPos.Y + 20, screen.Height) - posY;

                        if (Win32.FindImage(screen, _monsterBmp, posX, posY, width, height, ref point)
                            || Win32.FindImage(screen, _monkeyBmp, posX, posY, width, height, ref point))
                        {
                            await MoveAndAttack(characterPos, point);
                        }
                        else
                        {
                            PickupThings();

                            stopWatch.Stop();
                            await FindLadderAndClimb(screen, characterPos, (int)stopWatch.ElapsedMilliseconds);
                        }
                    }
                    else
                    {
                        if (_climbing)
                        {
                             stopWatch.Stop();
                             _timeToClimb += (int)stopWatch.ElapsedMilliseconds;

                            if (_timeToClimb >= 3000 + 4000)
                            {
                                _timeToClimb = 0;
                                _climbing = false;
                                _input.Keyboard.KeyUp(DirectInputKeyCode.DIK_UP);

                                Random rand = new Random();
                                if (rand.NextDouble() >= 0.5)
                                    MoveCharacter(DirectInputKeyCode.DIK_LEFT);
                                else
                                    MoveCharacter(DirectInputKeyCode.DIK_RIGHT);
                            }
                        }

                        PickupThings();
                    }

                    EvadeEdges(screen);
                }

                _finished = true;
            }
        }

        private async Task FindLadderAndClimb(Bitmap screen, Point characterPos, int elapsed)
        {
            Stopwatch stopWatch = new Stopwatch();

            _timeToClimb += elapsed;

            if (_timeToClimb >= 3000)
            {
                Point point = new Point();

                int posX = Math.Max(characterPos.X - 5, 0);
                int posY = Math.Max(characterPos.Y - 70, 0);
                int width = Math.Min(characterPos.X + 5, screen.Width) - posX;
                int height = Math.Min(posY + 45, screen.Height) - posY;

                bool climbing = Win32.FindImage(screen, _climbBmp, posX, posY, width, height, ref point);
                if (_climbing || climbing)
                {
                    if (!climbing)
                    {
                        if (_timeToClimb >= 3000 + 4000)
                        {
                            _timeToClimb = 0;
                            _climbing = false;
                            _input.Keyboard.KeyUp(DirectInputKeyCode.DIK_UP);

                            Random rand = new Random();
                            if (rand.NextDouble() >= 0.5)
                                MoveCharacter(DirectInputKeyCode.DIK_LEFT);
                            else
                                MoveCharacter(DirectInputKeyCode.DIK_RIGHT);
                        }
                    }
                    else
                    {
                        _input.Keyboard.KeyDown(DirectInputKeyCode.DIK_UP);
                    }
                }
                else
                {
                    posX = Math.Max(characterPos.X - 40, 0);
                    posY = Math.Max(characterPos.Y - 110, 0);
                    width = Math.Min(characterPos.X + 40, screen.Width) - posX;
                    height = Math.Min(posY + 75, screen.Height) - posY;

                    if (Win32.FindImage(screen, _ladderBmp, posX, posY, width, height, ref point))
                    {
                        if (Math.Abs(characterPos.X - point.X) <= 5)
                        {
                            StopMove();
                            await Task.Delay(500);

                            _input.Keyboard.KeyDown(DirectInputKeyCode.DIK_UP);
                            await Task.Delay(500);

                            _input.Keyboard.KeyPress(DirectInputKeyCode.DIK_D);
                            await Task.Delay(1500);

                            _climbing = true;
                            _timeToClimb = 3000;
                        }
                        else if (point.X < characterPos.X)
                        {
                            _input.Keyboard.KeyUp(DirectInputKeyCode.DIK_RIGHT);

                            _input.Keyboard.KeyDown(DirectInputKeyCode.DIK_LEFT);
                            await Task.Delay(25);
                            _input.Keyboard.KeyUp(DirectInputKeyCode.DIK_LEFT);

                            _arrowKey = DirectInputKeyCode.DIK_0;
                        }
                        else
                        {
                            _input.Keyboard.KeyUp(DirectInputKeyCode.DIK_RIGHT);

                            _input.Keyboard.KeyDown(DirectInputKeyCode.DIK_RIGHT);
                            await Task.Delay(25);
                            _input.Keyboard.KeyUp(DirectInputKeyCode.DIK_RIGHT);

                            _arrowKey = DirectInputKeyCode.DIK_0;
                        }
                    }
                    else
                    {
                        if (_arrowKey == DirectInputKeyCode.DIK_0)
                        {
                            Random rand = new Random();
                            if (rand.NextDouble() >= 0.5)
                                MoveCharacter(DirectInputKeyCode.DIK_LEFT);
                            else
                                MoveCharacter(DirectInputKeyCode.DIK_RIGHT);
                        }
                    }
                }
            }

            stopWatch.Stop();
            _timeToClimb += (int)stopWatch.ElapsedMilliseconds;
        }

        private async Task MoveAndAttack(Point characterPos, Point monsterPos)
        {
            var oldArrow = _arrowKey;

            if (monsterPos.X < characterPos.X)
                MoveCharacter(DirectInputKeyCode.DIK_LEFT);
            else
                MoveCharacter(DirectInputKeyCode.DIK_RIGHT);

            if (_arrowKey != oldArrow)
                await Task.Delay(200);

            _input.Keyboard.KeyPress(DirectInputKeyCode.DIK_S);
            _timeToClimb = 0;
        }

        private void EvadeEdges(Bitmap screen)
        {
            if (_arrowKey == DirectInputKeyCode.DIK_LEFT)
            {
                Point point = new Point();

                if (Win32.FindImage(screen, _characterBmp2, 6, 86, 14, 45, ref point)
                    || Win32.FindImage(screen, _characterBmp2, 61, 86, 9, 32, ref point))
                {
                    MoveCharacter(DirectInputKeyCode.DIK_RIGHT);
                }
            }
            else if (_arrowKey == DirectInputKeyCode.DIK_RIGHT)
            {
                Point point = new Point();

                if (Win32.FindImage(screen, _characterBmp2, 103, 86, 12, 45, ref point)
                    || Win32.FindImage(screen, _characterBmp2, 53, 86, 8, 32, ref point))
                {
                    MoveCharacter(DirectInputKeyCode.DIK_LEFT);
                }
            }
        }

        private void HotKey_Pressed(object sender, HotKeyEventArgs e)
        {
            ToggleProgram();
        }

        private void ToggleProgram()
        {
            if (_working)
                StopProgram();
            else
                StartProgram();
        }

        private void StartProgram()
        {
            _handle = Win32.FindWindow("MapleStoryClass", null);

            if (CheckGameWindow())
            {
                _working = true;
                button1.Text = "Stop";

                timer1.Start();
                timer2.Start();
                timer3.Start();

                Task.Run(() => UsePassiveSkills());
            }
        }

        private void StopProgram()
        {
            _working = false;
            button1.Text = "Start";

            timer1.Stop();
            timer2.Stop();
            timer3.Stop();
        }

        private async Task UsePassiveSkills()
        {
            _finished = false;

            await Task.Delay(1000);

            _input.Keyboard.KeyPress(DirectInputKeyCode.DIK_R);
            await Task.Delay(1000);

            _input.Keyboard.KeyPress(DirectInputKeyCode.DIK_T);
            await Task.Delay(1000);

            _input.Keyboard.KeyPress(DirectInputKeyCode.DIK_Y);
            await Task.Delay(1000);

            _finished = true;
        }

        private void PickupThings()
        {
            _input.Keyboard.KeyPress(DirectInputKeyCode.DIK_Z);
        }

        private void MoveCharacter(DirectInputKeyCode key)
        {
            if (key == DirectInputKeyCode.DIK_LEFT)
                _input.Keyboard.KeyUp(DirectInputKeyCode.DIK_RIGHT);
            else
                _input.Keyboard.KeyUp(DirectInputKeyCode.DIK_LEFT);

            _arrowKey = key;
            _input.Keyboard.KeyDown(key);
        }

        private void StopMove()
        {
            _input.Keyboard.KeyUp(DirectInputKeyCode.DIK_RIGHT);
            _input.Keyboard.KeyUp(DirectInputKeyCode.DIK_LEFT);
        }

        private bool CheckGameWindow(bool msgBox = true)
        {
            if (!Win32.IsWindowVisible(_handle))
            {
                if (msgBox)
                {
                    MessageBox.Show("Couldn't find MapleStory game client.");
                }

                return false;
            }

            return true;
        }
    }
}
