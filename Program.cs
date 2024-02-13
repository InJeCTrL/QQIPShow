using PacketDotNet;
using SharpPcap;

byte[] QQ_PHONE_PACKET_FEATURE = {0x02, 0x00, 0x48, 0x00, 0x01};

string previousIPAddress = "0.0.0.0";
HttpClient httpClient = new();

async Task<string> GetLocationInfo(string ipAddr) {
    var response = await httpClient.GetAsync($"http://ip-api.com/line/{ipAddr}?fields=49689&lang=zh-CN");
    var content = response.Content.ToString();
    if (content == null || !content.StartsWith("success")) {
        return "";
    }

    return content[content.IndexOf("\n")..];
}

void OnPacketArrival(object sender, PacketCapture e) {
    var rawPacket = e.GetPacket();
    var parsedPacket = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
    IPPacket ipPacket = (IPPacket)parsedPacket.PayloadPacket;
    var ipPacketBytes = ipPacket.Bytes;
    var srcIP = ipPacket.SourceAddress.ToString();
    UdpPacket udpPacket = (UdpPacket)ipPacket.PayloadPacket;
    var srcPort = udpPacket.SourcePort;
    var destPort = udpPacket.DestinationPort;

    // fast return if IP address is internal
    if (srcIP.StartsWith("192.168.") || srcIP.StartsWith("10.") ||
        srcIP.StartsWith("172.16.") || srcIP.StartsWith("172.31.")) {
        return;
    }

    // fast return if IP address is the same with previous one
    if (previousIPAddress == srcIP) {
        return;
    }

    for (int idxByte = 0; idxByte < ipPacketBytes.Length - QQ_PHONE_PACKET_FEATURE.Length; ++idxByte) {
        bool featureFound = true;
        for (int idxFeatureByte = 0; idxFeatureByte < QQ_PHONE_PACKET_FEATURE.Length; ++idxFeatureByte) {
            if (ipPacketBytes[idxByte + idxFeatureByte] != QQ_PHONE_PACKET_FEATURE[idxFeatureByte]) {
                featureFound = false;
                break;
            }
        }

        if (!featureFound) {
            continue;
        }

        Console.WriteLine($"IP Address: {srcIP}\n{GetLocationInfo(srcIP).Result}");
    }
}

foreach (var dev in CaptureDeviceList.Instance) {
    try {
        dev.Open(DeviceModes.Promiscuous, 0);
        dev.OnPacketArrival += OnPacketArrival;
        dev.Filter = "udp";
        dev.StartCapture();
    } catch {}
}

Console.WriteLine("Monitor is activated, press any key to exit.");
Console.ReadKey();

foreach (var dev in CaptureDeviceList.Instance) {
    try {
        dev.StopCapture();
    } finally {
        dev.Close();
    }
}