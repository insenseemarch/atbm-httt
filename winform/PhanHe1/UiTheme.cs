using System.Drawing;
using System.Windows.Forms;

namespace PhanHe1
{
    internal static class UiTheme
    {
        public static readonly Color DeepBlue = Color.FromArgb(12, 78, 140);      // #0C4E8C
        public static readonly Color BrandeisBlue = Color.FromArgb(12, 129, 228); // #0C81E4
        public static readonly Color JordyBlue = Color.FromArgb(17, 196, 212);    // #11C4D4
        public static readonly Color PastelGreen = Color.FromArgb(79, 231, 175);  // #4FE7AF
        public static readonly Color LightCyan = Color.FromArgb(255, 255, 255);
        public static readonly Color ZucchiniGreen = Color.FromArgb(12, 78, 140);
        public static readonly Color WhiteText = Color.FromArgb(245, 250, 248);
        public static readonly Color DarkText = Color.FromArgb(12, 78, 140);

        public static readonly Font BodyFont = new Font("Segoe UI", 10F, FontStyle.Regular);
        public static readonly Font HeaderFont = new Font("Segoe UI", 11F, FontStyle.Bold);

        public static void StylePrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = PastelGreen;
            button.ForeColor = DeepBlue;
            button.Font = HeaderFont;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(102, 235, 188);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(44, 211, 152);
        }

        public static void StyleSecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = BrandeisBlue;
            button.ForeColor = WhiteText;
            button.Font = HeaderFont;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 147, 232);
            button.FlatAppearance.MouseDownBackColor = DeepBlue;
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = LightCyan;
            grid.BorderStyle = BorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = BrandeisBlue;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = WhiteText;
            grid.ColumnHeadersDefaultCellStyle.Font = HeaderFont;
            grid.ColumnHeadersHeight = 40;
            grid.DefaultCellStyle.Font = BodyFont;
            grid.DefaultCellStyle.BackColor = LightCyan;
            grid.DefaultCellStyle.SelectionBackColor = JordyBlue;
            grid.DefaultCellStyle.SelectionForeColor = DeepBlue;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(236, 252, 248);
            grid.GridColor = Color.FromArgb(184, 236, 225);
        }
    }
}
