using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Formulario
{
    public partial class PreviewForm : Form
    {
        private RichTextBox richTextBoxPreview;
        private Button btnAceptar;
        private Button btnCancelar;

        public PreviewForm(string preview)
        {
            this.Text = "Preview de Ruta";
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // RichTextBox con mejor formato
            richTextBoxPreview = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Both,
                Text = preview,
                Font = new Font("Consolas", 10),
                BackColor = Color.White,
                ForeColor = Color.Black,
                Margin = new Padding(10)
            };
            this.Controls.Add(richTextBoxPreview);

            // Panel para botones
            Panel panelBotones = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.WhiteSmoke,
                Padding = new Padding(10)
            };

            // Botón Aceptar
            btnAceptar = new Button
            {
                Text = "Aceptar",
                Location = new Point(200, 15),
                Size = new Size(120, 35),
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Yes
            };
            panelBotones.Controls.Add(btnAceptar);

            // Botón Cancelar
            btnCancelar = new Button
            {
                Text = "Cancelar",
                Location = new Point(340, 15),
                Size = new Size(120, 35),
                BackColor = Color.Red,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.No
            };
            panelBotones.Controls.Add(btnCancelar);

            this.Controls.Add(panelBotones);
        }
    }
}
