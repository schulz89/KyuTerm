using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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

        public MainWindow()
        {
            InitializeComponent();
            serialPort = new SerialPort();
            serialPort.DataReceived += OnDataReceived;

            // Recover a list of current COM ports
            string[] ports = SerialPort.GetPortNames();
            var portsOrdered = ports.OrderBy(port => Convert.ToInt32(port.Replace("COM", string.Empty)));
            foreach (string port in portsOrdered)
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

                Terminal.AppendText(System.Text.Encoding.Default.GetString(buffer));

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
                    writer.Write(System.Text.Encoding.Default.GetString(buffer));
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
    }
}
