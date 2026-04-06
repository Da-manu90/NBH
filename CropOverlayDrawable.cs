using Microsoft.Maui.Graphics;

namespace NBH
{
    public class CropOverlayDrawable : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            // 1) Gesamtes Rechteck abgedunkelt
            //    => halbtransparent Schwarz
            canvas.FillColor = Colors.Black.WithAlpha(0.5f);
            canvas.FillRectangle(dirtyRect);

            // 2) Kreis in der Mitte frei machen
            //    => Durchmesser hier z.B. 200 px 
            float diameter = 200f;

            float centerX = dirtyRect.X + dirtyRect.Width / 2;
            float centerY = dirtyRect.Y + dirtyRect.Height / 2;

            float left = centerX - diameter / 2;
            float top = centerY - diameter / 2;
            float right = centerX + diameter / 2;
            float bottom = centerY + diameter / 2;

            RectF circleRect = new RectF(left, top, right - left, bottom - top);

            canvas.SaveState();
            // BlendMode.Clear => Inneres wird „freigeräumt“
            canvas.BlendMode = BlendMode.Clear;
            canvas.FillEllipse(circleRect);
            canvas.RestoreState();
        }
    }
}