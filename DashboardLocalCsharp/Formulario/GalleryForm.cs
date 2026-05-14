using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Formulario
{
    public partial class GalleryForm : Form
    {
        private List<string> photoPaths;
        private string photosFolder;
        private int currentIndex = 0;

        private PictureBox previewBox;
        private FlowLayoutPanel thumbnailPanel;
        private Label infoLabel;
        private Button prevBtn, nextBtn, openFolderBtn, deleteBtn;

        public GalleryForm(List<string> photoPaths, string photosFolder)
        {
            this.photoPaths = photoPaths;
            this.photosFolder = photosFolder;
            InitGallery();
            if (photoPaths.Count > 0) MostrarFoto(0);
        }

        private void InitGallery()
        {
            this.Text = "Galería de fotos";
            this.Size = new Size(900, 650);
            this.BackColor = Color.FromArgb(30, 30, 30);

            previewBox = new PictureBox
            {
                Size = new Size(580, 430),
                Location = new Point(10, 10),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };
            this.Controls.Add(previewBox);

            infoLabel = new Label
            {
                Location = new Point(10, 448),
                Size = new Size(580, 24),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(infoLabel);

            prevBtn = new Button { Text = "◀", Location = new Point(10, 478), Size = new Size(60, 32) };
            nextBtn = new Button { Text = "▶", Location = new Point(530, 478), Size = new Size(60, 32) };
            prevBtn.Click += (s, e) => MostrarFoto(currentIndex - 1);
            nextBtn.Click += (s, e) => MostrarFoto(currentIndex + 1);
            this.Controls.Add(prevBtn);
            this.Controls.Add(nextBtn);

            openFolderBtn = new Button
            {
                Text = "Abrir carpeta",
                Location = new Point(200, 478),
                Size = new Size(120, 32)
            };
            openFolderBtn.Click += (s, e) =>
                System.Diagnostics.Process.Start("explorer.exe", photosFolder);
            this.Controls.Add(openFolderBtn);

            deleteBtn = new Button
            {
                Text = "Eliminar",
                Location = new Point(330, 478),
                Size = new Size(90, 32),
                BackColor = Color.FromArgb(180, 50, 50),
                ForeColor = Color.White
            };
            deleteBtn.Click += DeleteBtn_Click;
            this.Controls.Add(deleteBtn);

            var thumbLabel = new Label
            {
                Text = "Miniaturas",
                Location = new Point(600, 10),
                Size = new Size(270, 20),
                ForeColor = Color.White
            };
            this.Controls.Add(thumbLabel);

            thumbnailPanel = new FlowLayoutPanel
            {
                Location = new Point(600, 35),
                Size = new Size(278, 520),
                AutoScroll = true,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            this.Controls.Add(thumbnailPanel);

            CargarMiniaturas();
        }

        private void CargarMiniaturas()
        {
            thumbnailPanel.Controls.Clear();
            for (int i = 0; i < photoPaths.Count; i++)
            {
                int idx = i;
                // ✅ Carga miniatura pequeña — libera el stream inmediatamente
                Image thumb;
                using (var bmp = new Bitmap(photoPaths[i]))
                    thumb = new Bitmap(bmp, new Size(120, 90));

                var thumbBox = new PictureBox
                {
                    Size = new Size(120, 90),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = thumb,
                    BackColor = Color.Black,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(4)
                };
                thumbBox.Click += (s, e) => MostrarFoto(idx);
                thumbnailPanel.Controls.Add(thumbBox);
            }
        }

        private void MostrarFoto(int index)
        {
            if (photoPaths.Count == 0) return;
            currentIndex = Math.Max(0, Math.Min(index, photoPaths.Count - 1));

            // ✅ Carga solo la foto que se va a mostrar y libera la anterior
            var oldImage = previewBox.Image;
            using (var bmp = new Bitmap(photoPaths[currentIndex]))
                previewBox.Image = new Bitmap(bmp);
            oldImage?.Dispose();

            infoLabel.Text = $"Foto {currentIndex + 1} de {photoPaths.Count}";
            prevBtn.Enabled = currentIndex > 0;
            nextBtn.Enabled = currentIndex < photoPaths.Count - 1;

            for (int i = 0; i < thumbnailPanel.Controls.Count; i++)
                thumbnailPanel.Controls[i].BackColor =
                    i == currentIndex ? Color.DodgerBlue : Color.Black;
        }

        private void DeleteBtn_Click(object sender, EventArgs e)
        {
            if (photoPaths.Count == 0) return;
            var result = MessageBox.Show("¿Eliminar esta foto?", "Confirmar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            // ✅ Elimina del disco también
            try { File.Delete(photoPaths[currentIndex]); } catch { }

            photoPaths.RemoveAt(currentIndex);
            CargarMiniaturas();

            if (photoPaths.Count == 0)
            {
                previewBox.Image = null;
                infoLabel.Text = "No hay fotos";
            }
            else
            {
                MostrarFoto(Math.Min(currentIndex, photoPaths.Count - 1));
            }
        }
    }
}