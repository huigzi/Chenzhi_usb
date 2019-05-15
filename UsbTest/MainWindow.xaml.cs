using LibUsbDotNet;
using LibUsbDotNet.Main;
using System;
using System.IO;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using MathNet.Numerics.Data.Matlab;
using MathNet.Numerics.LinearAlgebra;

namespace UsbTest
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    /// 

    internal class FilePath
    {
        private string _path = string.Empty;
        public string Path
        {
            get
            {
                if (_path.Length == 0)
                {
                    _path = "";
                }
                return _path;
            }
            set => _path = value;
        }
    }

    public enum DataState
    {
        Started, Stoped
    }

    public partial class MainWindow : System.Windows.Window
    {
        private UsbDevice MyUsbDevice;
        private UsbDeviceFinder MyUsbFinder = new UsbDeviceFinder(0x0483, 0x577F);//UsbDeviceFinder(0x0483, 0x572B);//秦旭阳板子(1155, 22399);// 姚成老板子 new UsbDeviceFinder(1155, 22315);
        private UsbRegDeviceList regList;
        private UsbEndpointReader reader;
        private string filename;
        private FileStream _filestream;
        private BinaryWriter _sw;
        private FilePath filePath = new FilePath();
        private DataState dataState = DataState.Stoped;
        private int count1, count2;

        private readonly ActionBlock<byte[]> _action;
        private readonly Algorithm _algorithm;


        private delegate void ShowMsg();
        private delegate void UpdateBytesDelegate(byte[] data);


        private void Window_Loaded(object sender, EventArgs e)
        {
    
            UsbFind();
            
        }

        private void Window_Closing(object sender, EventArgs e)
        {
            if (MyUsbDevice != null)
            {
                if (MyUsbDevice.IsOpen)
                {
                    try
                    {
                        IUsbDevice wholeUsbDevice = MyUsbDevice as IUsbDevice;
                        if (!(wholeUsbDevice is null))
                        {
                            wholeUsbDevice.ReleaseInterface(0);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Write(ex.ToString());
                    }
                }
                MyUsbDevice = null;
                UsbDevice.Exit();
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            framNum.Text = Properties.Settings.Default.frame;
            _action = new ActionBlock<byte[]>(x => Process(x));

            _algorithm = new Algorithm();
        }

        private void Process(byte[] data)
        {

            double[,] signal = new double[720, 5];

            for (int i = 0; i < 720; i++)
            {
                signal[i, 0] = BitConverter.ToInt16(data, i * 16 + 4);
                signal[i, 1] = BitConverter.ToInt16(data, i * 16 + 6);
                signal[i, 2] = BitConverter.ToInt16(data, i * 16 + 8);
                signal[i, 3] = BitConverter.ToInt16(data, i * 16 + 10);
                signal[i, 4] = BitConverter.ToInt16(data, i * 16 + 12);
            }

            var signalInput = Matrix<double>.Build.DenseOfArray(signal).SubMatrix(199, 520, 0, 5);

            //手势计算
            _algorithm.GestureCalculate(signalInput);

            Dispatcher.Invoke(() =>
            {
                if (_algorithm.StartFlag == false)
                {
                    Status.Text = "Stop";
                }
                else
                {
                    Status.Text = "Start";
                }

                Slider1.Value = _algorithm.Volume;

            });

        }

        private void OnRxEndPointData(object sender, EndpointDataEventArgs e)
        {
            AddMsg($" > {e.Count} data received");

            _action.Post(e.Buffer);

            if (dataState == DataState.Started)
            {
                Dispatcher.Invoke(new UpdateBytesDelegate(SaveData), e.Buffer);
            }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            UsbFind();
        }

        private void UsbFind()
        {
            regList = UsbDevice.AllDevices.FindAll(MyUsbFinder);
            if (regList.Count == 0)
            {
                System.Windows.MessageBox.Show("Device Not Found");
            }

            foreach(UsbRegistry regDevice in regList)
            {
                AddMsg($"{regDevice.Name} Connected");
            }

        }

        private void SaveData(byte[] bytes)
        {

            if (filePath.Path != "")
            {
                _sw.Write(bytes);
                _sw.Flush();
            }

            count2--;
            if(count2 <= 0)
            {
                dataState = DataState.Stoped;
                _sw.Dispose();
                _filestream.Dispose();
                scanButton.IsEnabled = true;
                connectbutton.IsEnabled = true;
                pathBotton.IsEnabled = true;
                savButton.IsEnabled = true;
            }

        }

        private void AddMsg(string msg)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ShowMsg)delegate () {
                if (lbxMsg.Items.Count > 50)
                {
                    lbxMsg.Items.RemoveAt(0);
                }
                lbxMsg.Items.Add(msg);
            });
        }

        private void ConnectbuttonClick(object sender, RoutedEventArgs e)
        {

            if ((bool)connectbutton.IsChecked)
            {
                if(MyUsbDevice != null)
                {

                }
                else
                {
                    if (regList.Count != 0)
                    {
                        foreach (UsbRegistry regDevice in regList)
                        {
                            AddMsg($"{regDevice.Name} Connected");
                        }

                        MyUsbDevice = UsbDevice.OpenUsbDevice(MyUsbFinder);
                        IUsbDevice wholeUsbDevice = MyUsbDevice as IUsbDevice;

                        if (!(wholeUsbDevice is null))
                        {
                            wholeUsbDevice.SetConfiguration(1);
                            wholeUsbDevice.ClaimInterface(0);
                        }

                        reader = MyUsbDevice.OpenEndpointReader(ReadEndpointID.Ep01);
                        reader.DataReceived += OnRxEndPointData;
                        reader.ReadBufferSize = 11520;//13760;
                        reader.Reset();
                        reader.DataReceivedEnabled = true;
                        scanButton.IsEnabled = false;
                        pathBotton.IsEnabled = true;
                        savButton.IsEnabled = true;
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Device Not Found");
                        connectbutton.IsChecked = false;
                    }
                }
            }
            else
            {
                if(MyUsbDevice != null)
                {

                    reader.DataReceivedEnabled = false;
                    reader.DataReceived -= OnRxEndPointData;

                    AddMsg($" > Device Disconnected");
                    AddMsg($" > Close Device");

                    if (MyUsbDevice.IsOpen)
                    {
                        try
                        {
                            IUsbDevice wholeUsbDevice = MyUsbDevice as IUsbDevice;
                            if(!(wholeUsbDevice is null))
                            {
                                wholeUsbDevice.ReleaseInterface(0);
                            }
                        }
                        catch(Exception ex)
                        {
                            Console.Write(ex.ToString());
                        }
                    }
                    MyUsbDevice = null;
                    UsbDevice.Exit();
                    scanButton.IsEnabled = true;
                    pathBotton.IsEnabled = false;
                    savButton.IsEnabled = false;

                    System.Windows.MessageBox.Show("Need Repower");
                }
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var mDialog = new FolderBrowserDialog();
            DialogResult result = mDialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.Cancel)
            {
                return;
            }

            filePath.Path = mDialog.SelectedPath.Trim();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var btn = (System.Windows.Controls.Button)sender;
            if(filePath.Path == "")
            {
                var mDialog = new FolderBrowserDialog();
                DialogResult result = mDialog.ShowDialog();

                if(result == System.Windows.Forms.DialogResult.Cancel)
                {
                    return;
                }

                filePath.Path = mDialog.SelectedPath.Trim();
            }

            count1++;
            count2 = int.Parse(framNum.Text.Trim());
            filename = DateTime.Now.ToString("MM-dd第") + count1.ToString() + "次数据";
            dataState = DataState.Started;
            string path = filePath.Path + "\\" + filename;
            _filestream = new FileStream(@path, FileMode.Create, FileAccess.Write);
            _sw = new BinaryWriter(_filestream);

            scanButton.IsEnabled = false;
            connectbutton.IsEnabled = false;
            pathBotton.IsEnabled = false;
            savButton.IsEnabled = false;
        }
    }
}
