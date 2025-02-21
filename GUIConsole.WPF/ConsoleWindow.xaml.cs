using GUIConsole.ConPTY;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GUIConsole.Wpf
{
    public partial class ConsoleWindow : UserControl
    {
        private Terminal _terminal;

        public ConsoleWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 初期カレントディレクトリ
            string currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            // Start up the console, and point it to cmd.exe.
            _terminal = new Terminal();
            Task.Run(() => _terminal.Start("powershell.exe", currentDirectory));
            _terminal.OutputReady += Terminal_OutputReady;
        }

        private void Terminal_OutputReady(object sender, EventArgs e)
        {
            // Start a long-lived thread for the "read console" task, so that we don't use a standard thread pool thread.
            Task.Factory.StartNew(() => CopyConsoleToWindow(), TaskCreationOptions.LongRunning);

            Dispatcher.Invoke(() => { TitleBarTitle.Text = "GUIConsole - powershell.exe"; });
        }

        private void CopyConsoleToWindow()
        {
            using (StreamReader reader = new StreamReader(_terminal.ConsoleOutStream))
            {
                // Read the console's output 1 character at a time
                int bytesRead;
                char[] buf = new char[1];
                while ((bytesRead = reader.ReadBlock(buf, 0, 1)) != 0)
                {
                    // This is where you'd parse and tokenize the incoming VT100 text, most likely.
                    Dispatcher.Invoke(() =>
                    {
                        // ...and then you'd do something to render it.
                        // For now, just emit raw VT100 to the primary TextBlock.
                        TerminalHistoryBlock.Text += new string(buf.Take(bytesRead).ToArray());
                    });
                }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                // This is where you'd take the pressed key, and convert it to a 
                // VT100 code before sending it along. For now, we'll just send _something_.
                _terminal.WriteToPseudoConsole(e.Key.ToString());
            }
        }

        private bool _autoScroll = true;
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // User scrolled...
            if (e.ExtentHeightChange == 0)
            {
                //...down to the bottom. Re-engage autoscrolling.
                if (TerminalHistoryViewer.VerticalOffset == TerminalHistoryViewer.ScrollableHeight)
                {
                    _autoScroll = true;
                }
                //...elsewhere. Disengage autoscrolling.
                else
                {
                    _autoScroll = false;
                }

                // Autoscrolling is enabled, and content caused scrolling:
                if (_autoScroll && e.ExtentHeightChange != 0)
                {
                    TerminalHistoryViewer.ScrollToEnd();
                }
            }
        }
    }
}
