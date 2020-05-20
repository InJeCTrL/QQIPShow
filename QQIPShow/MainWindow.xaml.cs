using PacketDotNet;
using SharpPcap;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Windows;

namespace QQIPShow
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// IP信息
        /// </summary>
        public class IPInfo
        {
            // IP地址
            public string IP { get; set; }
            // 地理位置
            public string Geo { get; set; }
            // 运营商
            public string ISP { get; set; }
            // 源端口
            public int SrcPort { get; set; }
            // 目的端口
            public int DestPort { get; set; }
        };
        // IP信息列表
        private ObservableCollection<IPInfo> IPList = new ObservableCollection<IPInfo>();
        /// <summary>
        /// 初始化时开始监听
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            IPGrid.DataContext = IPList;
            foreach (var dev in CaptureDeviceList.Instance)
            {
                dev.OnPacketArrival += Dev_OnPacketArrival;
                dev.Open(DeviceMode.Promiscuous, 0);
                dev.Filter = "udp";
                dev.StartCapture();
            }
        }
        /// <summary>
        /// 创建IP信息对象
        /// </summary>
        /// <param name="IP">IP地址</param>
        /// <param name="SrcPort">源端口</param>
        /// <param name="DestPort">目的端口</param>
        /// <returns>IP信息对象</returns>
        private IPInfo CreateIPInfo(string IP, int SrcPort, int DestPort)
        {
            WebRequest request = WebRequest.Create("http://ip-api.com/line/" + IP + "?fields=49689&lang=zh-CN");
            IPInfo iPInfo = new IPInfo
            {
                IP = IP,
                SrcPort = SrcPort,
                DestPort = DestPort
            };
            using (WebResponse response = request.GetResponse())
            {
                Stream s = response.GetResponseStream();
                StreamReader sr = new StreamReader(s);
                // IP地址数据是否有效
                string str_status = sr.ReadLine();
                if (str_status.Equals("success") == true)
                {
                    // 国家
                    iPInfo.Geo += sr.ReadLine();
                    // 区域
                    iPInfo.Geo += sr.ReadLine();
                    // 城市
                    iPInfo.Geo += sr.ReadLine();
                    // 网络供应商
                    iPInfo.ISP = sr.ReadLine();
                }
            }
            return iPInfo;
        }
        /// <summary>
        /// 判断IP、源端口、目的端口是否与最后一条完全一致
        /// </summary>
        /// <param name="IP">IP地址</param>
        /// <param name="SrcPort">源端口</param>
        /// <param name="DestPort">目的端口</param>
        /// <returns></returns>
        private bool NotTheSame(string IP, int SrcPort, int DestPort)
        {
            if (IPList.Count == 0)
            {
                return true;
            }
            IPInfo LastIP = IPList[IPList.Count - 1];
            if (LastIP.IP.Equals(IP) && LastIP.SrcPort == SrcPort && LastIP.DestPort == DestPort)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        /// <summary>
        /// 处理收到的包
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Dev_OnPacketArrival(object sender, CaptureEventArgs e)
        {
            var ParsedPacket = Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
            Byte[] DataBytes = ((IPPacket)ParsedPacket.PayloadPacket).Bytes;
            string SrcIP = ((IPPacket)ParsedPacket.PayloadPacket).SourceAddress.ToString();
            int SrcPort = ((UdpPacket)((IPPacket)ParsedPacket.PayloadPacket).PayloadPacket).SourcePort;
            int DestPort = ((UdpPacket)((IPPacket)ParsedPacket.PayloadPacket).PayloadPacket).DestinationPort;
            int i_byte = 0;
            foreach (var DataByte in DataBytes)
            {
                if (DataBytes.Length > i_byte + 4 && DataByte.Equals(0x02) && DataBytes[i_byte + 1].Equals(0x0) && 
                    DataBytes[i_byte + 2].Equals(0x48) && DataBytes[i_byte + 3].Equals(0x0) && 
                    DataBytes[i_byte + 4].Equals(0x01))
                {
                    bool srcYES = !SrcIP.StartsWith("192");
                    if (srcYES && NotTheSame(SrcIP, SrcPort, DestPort))
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            IPList.Add(CreateIPInfo(SrcIP, SrcPort, DestPort));
                        });
                    }
                    break;
                }
                ++i_byte;
            }
        }
        /// <summary>
        /// 关闭程序结束监听
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (var dev in CaptureDeviceList.Instance)
            {
                try
                {
                    dev.StopCapture();
                }
                catch
                {
                    dev.Close();
                }
            }
        }
    }
}
