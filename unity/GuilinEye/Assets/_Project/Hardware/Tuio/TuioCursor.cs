using System;

namespace GuilinEye.Hardware
{
    /// <summary>TUIO 2Dcur / 2Dblb 的一个光标。坐标为 TUIO 归一化 0..1，原点左下。</summary>
    public class TuioCursor
    {
        public int Id;
        public float X, Y;
        public float VelocityX, VelocityY;
        public float Width, Height;
        public string Source;
        public long TimestampMs;

        public TuioCursor Clone()
        {
            var c = new TuioCursor();
            c.Id = Id; c.X = X; c.Y = Y; c.VelocityX = VelocityX; c.VelocityY = VelocityY;
            c.Width = Width; c.Height = Height; c.Source = Source; c.TimestampMs = TimestampMs;
            return c;
        }
    }
}
