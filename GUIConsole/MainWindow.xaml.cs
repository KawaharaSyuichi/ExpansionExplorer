using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit.Document;

namespace GUIConsole
{
    public partial class TerminalView : UserControl
    {
        // 現在の作業ディレクトリ（初期はアプリ起動ディレクトリ）
        private string currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // 現在の入力開始位置（プロンプト直後のオフセット）
        private int inputStartOffset;

        public TerminalView()
        {
            InitializeComponent();
            
            foreach (var binding in TerminalEditor.TextArea.InputBindings)
            {
                if (binding is KeyBinding keyBinding)
                {
                    Debug.WriteLine($"Key: {keyBinding.Key}, Modifiers: {keyBinding.Modifiers}, Command: {keyBinding.Command}");
                }
            }

            // 独自の入力バインディング処理を追加
            TerminalEditor.TextArea.InputBindings.Add(
                new KeyBinding(new RelayCommand(ExecuteInputCommand), Key.Enter, ModifierKeys.None));
            // Shift+Enter で改行を許可する
            TerminalEditor.TextArea.InputBindings.Add(
                new KeyBinding(new RelayCommand(() => InsertNewLine()), Key.Enter, ModifierKeys.Shift));

            // PreviewKeyDown でプロンプト以前の編集を防止
            TerminalEditor.TextArea.PreviewKeyDown += TerminalEditor_PreviewKeyDown;

            AppendPrompt();
            TerminalEditor.CaretOffset = TerminalEditor.Document.TextLength;
            TerminalEditor.Focus();
        }

        /// <summary>
        /// 出力領域の末尾にテキストを追加し、スクロールする
        /// </summary>
        /// <param name="text">追加するテキスト</param>
        private void AppendToTerminal(string text)
        {
            TerminalEditor.Document.Insert(TerminalEditor.Document.TextLength, text);
            TerminalEditor.ScrollToEnd();
        }

        /// <summary>
        /// プロンプト（例："C:\MyFolder> "）を追加し、入力開始位置を更新する
        /// </summary>
        private void AppendPrompt()
        {
            string prompt = $"{currentDirectory}> ";
            AppendToTerminal(prompt);
            inputStartOffset = TerminalEditor.Document.TextLength;
        }

        /// <summary>
        /// Shift+Enter の場合に改行を挿入する
        /// </summary>
        private void InsertNewLine()
        {
            int offset = TerminalEditor.CaretOffset;
            TerminalEditor.Document.Insert(offset, Environment.NewLine);
            TerminalEditor.CaretOffset = offset + Environment.NewLine.Length;
        }

        /// <summary>
        /// カスタム KeyBinding から呼び出される、コマンド実行のエントリポイント
        /// </summary>
        private async void ExecuteInputCommand()
        {
            await ProcessInput();
        }

        /// <summary>
        /// 入力部分（プロンプト以降）のテキストを取得し、コマンド実行結果を出力する
        /// </summary>
        private async Task ProcessInput()
        {
            int totalLength = TerminalEditor.Document.TextLength;
            // プロンプト以降のテキストを取得
            string command = TerminalEditor.Document.GetText(inputStartOffset, totalLength - inputStartOffset)
                                        .TrimEnd('\r', '\n');

            // コマンド行の末尾に改行を追加して確定
            AppendToTerminal(Environment.NewLine);

            // cls コマンドの場合は画面をクリアする
            if (command.Equals("cls", StringComparison.OrdinalIgnoreCase))
            {
                TerminalEditor.Document.Text = "";
                AppendPrompt();
                return;
            }

            // ドライブ指定のみの場合（例："D:" や "D:\"）の処理
            if (System.Text.RegularExpressions.Regex.IsMatch(command, @"^[A-Za-z]:[\\/]?$"))
            {
                string drive = command.Substring(0, 2); // "D:" など
                                                        // ドライブが存在するかをチェック（存在するなら "D:\" のような形式にする）
                if (System.IO.Directory.Exists(drive + "\\"))
                {
                    currentDirectory = drive + "\\";
                }
                else
                {
                    AppendToTerminal($"ドライブが存在しません: {drive}" + Environment.NewLine);
                }
                AppendPrompt();
                return;
            }

            // cd コマンドの場合の処理
            if (command.StartsWith("cd", StringComparison.OrdinalIgnoreCase))
            {
                // "cd" 単体の場合はヘルプメッセージを表示
                if (command.Trim().Equals("cd", StringComparison.OrdinalIgnoreCase))
                {
                    AppendToTerminal("使用法: cd <ディレクトリ>" + Environment.NewLine);
                    AppendPrompt();
                    return;
                }

                // "cd <target>" の形式とする（ドライブ指定はここでは処理しない）
                string target = command.Substring(2).Trim();
                // ドライブ指定の場合は処理をスキップ（または何もしない）
                if (System.Text.RegularExpressions.Regex.IsMatch(target, @"^[A-Za-z]:[\\/]?$"))
                {
                    AppendToTerminal("ドライブ指定は、単独で入力してください。" + Environment.NewLine);
                    AppendPrompt();
                    return;
                }

                // 相対パスの場合、現在のディレクトリと結合
                if (!System.IO.Path.IsPathRooted(target))
                {
                    target = System.IO.Path.Combine(currentDirectory, target);
                }

                if (System.IO.Directory.Exists(target))
                {
                    currentDirectory = System.IO.Path.GetFullPath(target);
                }
                else
                {
                    AppendToTerminal($"ディレクトリが存在しません: {target}" + Environment.NewLine);
                }
                AppendPrompt();
                return;
            }

            // コマンドが空の場合は次のプロンプトを表示
            if (string.IsNullOrWhiteSpace(command))
            {
                AppendPrompt();
                return;
            }

            // 非同期でコマンド実行
            string result = await Task.Run(() => ExecuteCommand(command));
            if (!string.IsNullOrEmpty(result))
            {
                AppendToTerminal(result + Environment.NewLine);
            }
            AppendPrompt();
        }


        /// <summary>
        /// cmd.exe を使用して指定のコマンドを実行し、標準出力・標準エラーの結果を返す
        /// </summary>
        private string ExecuteCommand(string command)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = currentDirectory
                };

                using (Process process = new Process())
                {
                    process.StartInfo = psi;
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return output + error;
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// PreviewKeyDown で、プロンプト部分（入力開始位置より前）の編集を防止する
        /// </summary>
        private void TerminalEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Enter キー（Shift なし）が押された場合、独自の処理を優先して実行する
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.None)
            {
                ExecuteInputCommand();
                e.Handled = true;
                return;
            }

            // カーソルが入力領域（inputStartOffset～）より前にある場合、入力領域の先頭に戻す
            if (TerminalEditor.CaretOffset < inputStartOffset)
            {
                TerminalEditor.CaretOffset = inputStartOffset;
            }
            // Backspace キーの場合、入力開始位置以下なら削除を無効化
            if (e.Key == Key.Back && TerminalEditor.CaretOffset <= inputStartOffset)
            {
                e.Handled = true;
            }
            // 選択範囲が入力開始位置より前に及んでいる場合も無効化
            if (TerminalEditor.SelectionLength > 0)
            {
                if (TerminalEditor.SelectionStart < inputStartOffset)
                {
                    e.Handled = true;
                }
            }
        }
    }
}
