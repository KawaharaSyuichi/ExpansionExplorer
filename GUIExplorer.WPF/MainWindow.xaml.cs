using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Web.WebView2.Core;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private bool _bUseTerminal;
        public bool bUseTerminal 
        {
            get { return _bUseTerminal; }
            set 
            {
                if (_bUseTerminal != value) 
                {
                    _bUseTerminal = value;
                    OnPropertyChanged();
                    UpdateGridRowDefinitions();
                }
            }
        }
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadDrives();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) 
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // ドライブ情報をTreeViewに追加する
        private void LoadDrives()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    TreeViewItem driveItem = new TreeViewItem
                    {
                        Header = drive.Name,
                        Tag = drive.RootDirectory.FullName
                    };

                    // 初期状態で子要素のプレースホルダーを追加(遅延読み込み要)
                    driveItem.Items.Add(null);
                    driveItem.Expanded += Folder_Expanded;
                    FolderTree.Items.Add(driveItem);
                }
            }
        }

        // フォルダーが展開された時に子要素(サブファイル)を読み込む
        private void Folder_Expanded(object sender, RoutedEventArgs s)
        {
            TreeViewItem? item = sender as TreeViewItem;
            if (item == null)
            {
                return;
            }

            if (item.Items.Count == 1 && item.Items[0] == null)
            {
                item.Items.Clear();
                string? fullPath = item.Tag as string;
                if (fullPath == null)
                {
                    return;
                }

                try
                {
                    foreach (string dir in Directory.GetDirectories(fullPath))
                    {
                        TreeViewItem subItem = new TreeViewItem
                        {
                            Header = System.IO.Path.GetFileName(dir),
                            Tag = dir
                        };

                        // 遅延読み込みのためにプレースホルダーを追加
                        subItem.Items.Add(null);
                        subItem.Expanded += Folder_Expanded;
                        item.Items.Add(subItem);
                    }
                }
                catch
                {
                }
            }
        }

        // TreeViewの選択が変化したときにListViewを更新する
        private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem? selectedItem = FolderTree.SelectedItem as TreeViewItem;
            if (selectedItem == null)
            {
                return;
            }
            else
            {
                string? selectedPath = selectedItem.Tag as string;
                if (selectedPath == null)
                {
                    return;
                }
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    LoadFiles(selectedPath);
                }
            }
        }

        // 選択されたフォルダ内のファイルとフォルダ情報をListViewに追加する
        private void LoadFiles(string path)
        {
            FileList.Items.Clear();

            // フォルダを先に追加する
            try
            {
                foreach (string dir in Directory.GetDirectories(path))
                {
                    DirectoryInfo di = new DirectoryInfo(dir);
                    FileList.Items.Add(new
                    {
                        Name = di.Name,
                        Type = "Folder",
                        Size = "",
                        Modified = di.LastWriteTime.ToString()
                    });
                }
            }
            catch
            {
            }

            // 次にファイルを追加する
            try
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    FileInfo fi = new FileInfo(file);
                    FileList.Items.Add(new
                    {
                        Name = fi.Name,
                        Type = fi.Extension,
                        Size = fi.Length.ToString(),
                        Modified = fi.LastWriteTime.ToString()
                    });
                }
            }
            catch
            {
            }
        }

        private void UpdateGridRowDefinitions()
        {
            if (bUseTerminal)
            {
                // ターミナルが表示される場合
                GridLengthConverter gridLengthConverter = new GridLengthConverter();
                var parentGrid = TerminalView.Parent as Grid;
                if (parentGrid != null)
                {
                    parentGrid.RowDefinitions[0].Height = (GridLength)gridLengthConverter.ConvertFromString("300")!;
                    parentGrid.RowDefinitions[2].Height = (GridLength)gridLengthConverter.ConvertFromString("*")!;
                }
            }
            else
            {
                // ターミナルが非表示の場合
                GridLengthConverter gridLengthConverter = new GridLengthConverter();
                var parentGrid = TerminalView.Parent as Grid;
                if (parentGrid != null)
                {
                    parentGrid.RowDefinitions[0].Height = (GridLength)gridLengthConverter.ConvertFromString("*")!;
                    parentGrid.RowDefinitions[2].Height = (GridLength)gridLengthConverter.ConvertFromString("0")!;
                }
            }
        }
    }
}