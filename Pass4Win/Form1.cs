﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using GpgApi;


namespace Pass4Win
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
            // Checking for appsettings
            // Pass directory
            if (Properties.Settings.Default.PassDirectory == "firstrun")
            {
                if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                {
                    Properties.Settings.Default.PassDirectory = folderBrowserDialog1.SelectedPath;
                    // TODO: Check what happens when directory is empy or not valid
                }
                else
                {
                    Application.Exit();
                }
            }

            // GPG exe location
            if (Properties.Settings.Default.GPGEXE == "firstrun")
            {
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    Properties.Settings.Default.GPGEXE = openFileDialog1.FileName;
                }
                else
                {
                    Application.Exit();
                }
            }
            // Setting the exe location for the GPG Dll
            GpgInterface.ExePath = Properties.Settings.Default.GPGEXE;

            // GPG key
            if (Properties.Settings.Default.GPGKey == "firstrun")
            {
                KeySelect newKeySelect = new KeySelect();
                if (newKeySelect.ShowDialog() == DialogResult.OK)
                {
                    Properties.Settings.Default.GPGKey = newKeySelect.gpgkey;
                }
            }

            // saving settings
            Properties.Settings.Default.Save();

            // Setting up datagrid
            dt.Columns.Add("colPath", typeof(string));
            dt.Columns.Add("colText", typeof(string));

            ListDirectory(new DirectoryInfo(Properties.Settings.Default.PassDirectory), "");

            dataPass.DataSource = dt.DefaultView;
            dataPass.Columns[0].Visible=false;
        }

        // Used for class access to the data
        private DataTable dt = new DataTable();

        // Fills the datagrid
        private void ListDirectory(DirectoryInfo path, string prefix)
        {
            foreach (var directory in path.GetDirectories())
            {
                if (!directory.Name.StartsWith("."))
                {
                    string tmpPrefix;
                    if (prefix != "")
                    {
                        tmpPrefix = prefix + "\\" + directory;
                    }
                    else
                    {
                        tmpPrefix = prefix + directory;
                    }
                    ListDirectory(directory, tmpPrefix);
                }
            }

            foreach (var ffile in path.GetFiles())
                if (!ffile.Name.StartsWith("."))
                {
                    // TODO: Check if it's a GPG file or not
                    DataRow newItemRow = dt.NewRow();

                    newItemRow["colPath"] = ffile.FullName;
                    newItemRow["colText"] = prefix + "\\" + Path.GetFileNameWithoutExtension(ffile.Name);

                    dt.Rows.Add(newItemRow);
                }
        }

        // Search handler
        private void txtPass_TextChanged(object sender, EventArgs e)
        {
                dt.DefaultView.RowFilter = "colText LIKE '%" + txtPass.Text + "%'";
        }

        // Decrypt the selected entry when pressing enter in textbox
        private void txtPass_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
            {
                decrypt_pass(dataPass.Rows[dataPass.CurrentCell.RowIndex].Cells[0].Value.ToString());
            }
        }

        // Class access to the tempfile
        private string tmpfile;

        // Decrypt the file into a tempfile. With async thread
        private void decrypt_pass(string path)
        {
            tmpfile = Path.GetTempFileName();
            GpgDecrypt decrypt = new GpgDecrypt(path, tmpfile);
            {
                    decrypt.ExecuteAsync(Callback);
            }
        }

        // Class function for the callback to use.
        private void AppendDecryptedtxt(string value)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(AppendDecryptedtxt), new object[] { value });
                return;
            }
            txtPassDetail.Text = value;
        }
        
        // Callback for the async thread
        private void Callback(GpgInterfaceResult result)
        {
            if (result.Status == GpgInterfaceStatus.Success)
            {
                AppendDecryptedtxt(File.ReadAllText(this.tmpfile));
                File.Delete(tmpfile);
            }
            else
            {
                AppendDecryptedtxt("Something went wrong.....");
            }
        }

        // If clicked in the datagrid then decrypt that entry
        private void dataPass_Click(object sender, EventArgs e)
        {
            decrypt_pass(dataPass.Rows[dataPass.CurrentCell.RowIndex].Cells[0].Value.ToString());
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // rename the entry
            InputBoxValidation validation = delegate(string val)
            {
                if (val == "")
                    return "Value cannot be empty.";
                if (new Regex(@"[a-zA-Z0-9-\\_]+/g").IsMatch(val))
                    return "Not a valid name, can only use characters or numbers and - \\.";
                return "";
            };

            string value = dataPass.Rows[dataPass.CurrentCell.RowIndex].Cells[1].Value.ToString();
            if (InputBox.Show("Enter a new name", "Name:", ref value, validation) == DialogResult.OK)
            {
                // parse path
                string tmpPath = Properties.Settings.Default.PassDirectory + "\\" + value + ".gpg";
                Directory.CreateDirectory(Path.GetDirectoryName(tmpPath));
                File.Move(dataPass.Rows[dataPass.CurrentCell.RowIndex].Cells[0].Value.ToString(), tmpPath);
                dt.Clear();
                processDirectory(Properties.Settings.Default.PassDirectory);
                ListDirectory(new DirectoryInfo(Properties.Settings.Default.PassDirectory), "");

            }

        }

        // cleanup script to remove empty directories from the password store
        private static void processDirectory(string startLocation)
        {
            foreach (var directory in Directory.GetDirectories(startLocation))
            {
                processDirectory(directory);
                if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                {
                    Directory.Delete(directory, false);
                }
            }
        }

    }

    public class InputBox
    {
        public static DialogResult Show(string title, string promptText, ref string value)
        {
            return Show(title, promptText, ref value, null);
        }

        public static DialogResult Show(string title, string promptText, ref string value,
                                        InputBoxValidation validation)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(9, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            buttonCancel.SetBounds(309, 72, 75, 23);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;
            if (validation != null)
            {
                form.FormClosing += delegate(object sender, FormClosingEventArgs e)
                {
                    if (form.DialogResult == DialogResult.OK)
                    {
                        string errorText = validation(textBox.Text);
                        if (e.Cancel = (errorText != ""))
                        {
                            MessageBox.Show(form, errorText, "Validation Error",
                                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                            textBox.Focus();
                        }
                    }
                };
            }
            DialogResult dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }
    }
    public delegate string InputBoxValidation(string errorMessage);
}

