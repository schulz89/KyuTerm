// Simple serial terminal for satellite debugging
// Copyright (C) 2023 Victor Hugo Schulz

// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.IO;
using System.IO.Ports;
using System.Collections.Specialized;
using System.Text.RegularExpressions;

namespace KyuTerm
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        StreamWriter writer = null;
        string filename = DateTime.Now.ToString("yyyyMMdd") + "_test.txt";
        const int minLines = 512;
        const int maxLines = 1024;
        static SerialPort serialPort;
        bool autoInsert = false;
        private DispatcherTimer clipboardTimer;

        public MainWindow()
        {
            InitializeComponent();
            serialPort = new SerialPort();
            serialPort.DataReceived += OnDataReceived;

            InitializeComponent();
            SetupClipboardMonitor();

            PopulateComPortList();
        }

        private void PopulateComPortList()
        {
            // Recover a list of current COM ports
            PortComboBox.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                PortComboBox.Items.Add(port);
            }
            PortComboBox.SelectedIndex = 0;
        }

        private void WindowClosed(object sender, EventArgs e)
        {
            if (writer != null)
            {
                writer.Close();
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var serialDevice = sender as SerialPort;
            var buffer = new byte[serialDevice.BytesToRead];
            serialDevice.Read(buffer, 0, buffer.Length);

            // process data on the GUI thread
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                StringCollection lines = new StringCollection();
                if(CheckBoxHexMode.IsChecked == false)
                {
                    Terminal.AppendText(System.Text.Encoding.Default.GetString(buffer));
                }
                else
                {
                    String message_print = BitConverter.ToString(buffer).Replace("-", "");
                    Terminal.AppendText(message_print);
                }

                // Makes sure data stored in RAM never exceeds maxLines
                int lineCount = Terminal.LineCount;
                if (Terminal.LineCount > maxLines)
                {
                    for (int line = lineCount - minLines; line < lineCount; line++)
                        lines.Add(Terminal.GetLineText(line));
                    Terminal.Clear();
                    foreach (string line in lines)
                    {
                        Terminal.AppendText(line);
                    }
                }
                Terminal.ScrollToEnd();
                if (writer != null)
                {
                    if (CheckBoxHexMode.IsChecked == false)
                    {
                        writer.Write(System.Text.Encoding.Default.GetString(buffer));
                    }
                    else
                    {
                        String message_print = BitConverter.ToString(buffer).Replace("-", "");
                        writer.Write(message_print);
                    }
                    writer.Flush();
                }
            }));
        }

        private void FileSelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (writer != null)
            {
                writer = null;
            }
            filename = DateTime.Now.ToString("yyyyMMdd") + "_test.txt";
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            saveFileDialog.FileName = filename;
            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (saveFileDialog.ShowDialog() == true)
                LogFileTextBox.Text = saveFileDialog.FileName;
            writer = new StreamWriter(saveFileDialog.FileName);
        }

        private void OpenCloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }
            else
            {
                if (int.TryParse(BaudTextBox.Text, out int baudRate))
                {
                    serialPort.PortName = PortComboBox.Text;
                    serialPort.BaudRate = baudRate;
                    serialPort.Open();
                }
            }

            if (serialPort.IsOpen)
            {
                OpenCloseButton.Content = "Close";
            }
            else
            {
                OpenCloseButton.Content = "Open";
            }
        }

        private void TextBox_PreviewTextInput_IsHex(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9a-fA-F ]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                byte[] command = StringToByteArray(CommandTextBox.Text);
                if (serialPort.IsOpen)
                {
                    serialPort.Write(command, 0, command.Length);
                }
                else
                {
                    MessageBox.Show("The serial connection is not open.");
                }
            }
        }

        public static byte[] StringToByteArray(string hex)
        {
            hex = Regex.Replace(hex, @"\s+", "");
            if (hex.Length % 2 == 1)
            {
                MessageBox.Show("The binary number should not have an odd number of digits.");
                byte[] arr = new byte[0];
                return arr;
            }
            else
            {
                byte[] arr = new byte[hex.Length >> 1];
                for (int i = 0; i < hex.Length >> 1; ++i)
                {
                    arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
                }
                return arr;
            }
        }

        public static int GetHexVal(char hex)
        {
            int val = (int)hex;
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

        private void CommandSendButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] command = StringToByteArray(CommandTextBox.Text);
            if (serialPort.IsOpen)
            {
                serialPort.Write(command, 0, command.Length);
            }
            else
            {
                MessageBox.Show("The serial connection is not open.");
            }
        }
        private void SetupClipboardMonitor()
        {
            clipboardTimer = new DispatcherTimer();
            clipboardTimer.Interval = TimeSpan.FromSeconds(1); // Check every second
            clipboardTimer.Tick += ClipboardTimer_Tick;
            clipboardTimer.Start();
        }

        private void ClipboardTimer_Tick(object sender, EventArgs e)
        {
            CheckClipboardForHexPattern();
        }

        private void CheckClipboardForHexPattern()
        {
            if (Clipboard.ContainsText())
            {
                string clipboardText = Clipboard.GetText();
                if (autoInsert && IsHexPattern(clipboardText))
                {
                    CommandTextBox.Text = clipboardText;
                }
            }
        }

        private bool IsHexPattern(string input)
        {
            // Pattern to match a hexadecimal string with or without spaces
            string pattern = @"^(([0-9A-Fa-f]{2}(\s)*)+)$";
            // Ensure the number of non-space characters is even
            return Regex.IsMatch(input, pattern) && input.Replace(" ", "").Length % 2 == 0;
        }

        private void CheckBoxHexMode_Checked(object sender, RoutedEventArgs e)
        {
            Terminal.Clear();
        }

        private void CheckBoxHexMode_Unchecked(object sender, RoutedEventArgs e)
        {
            Terminal.Clear();
        }

        private void PortComboBox_DropDownOpened(object sender, EventArgs e)
        {
            PopulateComPortList();
        }

        private void CheckBoxAutoInsert_Checked(object sender, RoutedEventArgs e)
        {
            autoInsert = true;
        }

        private void CheckBoxAutoInsert_Unchecked(object sender, RoutedEventArgs e)
        {
            autoInsert = false;
        }
    }
}
