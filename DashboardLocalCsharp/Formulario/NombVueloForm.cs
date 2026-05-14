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
    public partial class NombVueloForm : Form
    {
        private TextBox textBoxNombre;
        private Button btnAceptar;
        private Button btnCancelar;

        public NombVueloForm()
        {
            this.Text = "Nombre del Vuelo";
            this.Size = new Size(400, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Label
            Label lblNombre = new Label
            {
                Text = "Ingresa el nombre/nametag del vuelo:",
                Location = new Point(10, 10),
                AutoSize = true,
                Font = new Font("Arial", 10)
            };
            this.Controls.Add(lblNombre);

            // TextBox
            textBoxNombre = new TextBox
            {
                Location = new Point(10, 40),
                Size = new Size(360, 25),
                Font = new Font("Arial", 10),
                Text = $"Ruta_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"
            };
            this.Controls.Add(textBoxNombre);

            // Botón Aceptar
            btnAceptar = new Button
            {
                Text = "Aceptar",
                Location = new Point(150, 80),
                Size = new Size(100, 35),
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            this.Controls.Add(btnAceptar);

            // Botón Cancelar
            btnCancelar = new Button
            {
                Text = "Cancelar",
                Location = new Point(270, 80),
                Size = new Size(100, 35),
                BackColor = Color.Red,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(btnCancelar);

            this.AcceptButton = btnAceptar;
            this.CancelButton = btnCancelar;
        }

        public string ObtenerNombre()
        {
            return textBoxNombre.Text;
        }
    }
}
