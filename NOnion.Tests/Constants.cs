﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NOnion.Tests
{
    public static class FallbackDirectorySelector
    {

        // List copied from https://raw.githubusercontent.com/torproject/tor/main/src/app/config/fallback_dirs.inc
        private static readonly List<string> fallbackDirectories =
            new List<string>()
            {
                "46.126.96.8:9001",
                "85.214.141.24:9001",
                "185.76.191.83:443",
                "89.41.173.138:443",
                "142.4.213.88:443",
                "148.251.191.252:443",
                "51.89.143.155:443",
                "86.80.108.228:9001",
                "54.37.232.61:443",
                "185.76.191.76:443",
                "45.140.170.187:9001",
                "81.83.37.138:9001",
                "82.64.163.188:9001",
                "104.244.73.93:9000",
                "194.38.21.10:9001",
                "58.185.69.242:8443",
                "49.194.93.54:9001",
                "82.35.159.1:9001",
                "37.157.254.37:443",
                "78.46.85.216:9001",
                "104.244.74.28:9001",
                "142.4.213.112:443",
                "68.196.180.5:8443",
                "45.91.67.90:9001",
                "89.212.26.36:9001",
                "116.203.245.170:443",
                "185.220.101.13:30013",
                "37.221.198.114:9001",
                "163.172.194.53:9001",
                "185.220.100.243:9100",
                "198.245.49.6:443",
                "78.194.37.29:9001",
                "89.163.128.28:9001",
                "174.127.169.233:9001",
                "45.79.70.147:9001",
                "51.68.173.83:9001",
                "104.244.79.121:443",
                "147.135.112.139:9001",
                "37.187.98.185:22",
                "51.75.82.166:443",
                "185.86.150.58:9001",
                "91.219.236.197:443",
                "31.131.4.171:443",
                "209.141.40.46:443",
                "2.230.193.197:9003",
                "104.244.74.121:9100",
                "152.89.104.206:9001",
                "114.108.58.201:443",
                "68.67.32.32:9001",
                "107.189.30.230:9001",
                "51.178.26.103:443",
                "192.166.245.82:443",
                "95.217.15.17:443",
                "199.249.230.189:443",
                "109.70.100.15:443",
                "185.73.211.3:50001",
                "37.187.21.49:9001",
                "67.3.168.113:443",
                "5.189.148.225:9001",
                "213.183.60.21:443",
                "54.38.92.43:9001",
                "178.20.55.16:443",
                "37.187.102.108:443",
                "51.15.65.243:443",
                "188.126.83.38:443",
                "151.115.56.33:443",
                "185.220.101.137:10210",
                "46.194.163.28:65534",
                "85.214.196.178:9001",
                "23.129.64.133:443",
                "185.220.101.14:10014",
                "188.127.226.102:110",
                "45.33.46.101:9001",
                "116.203.23.183:9001",
                "140.78.100.43:8443",
                "78.46.202.212:9010",
                "80.100.69.97:9001",
                "89.163.128.26:9001",
                "38.39.192.78:443",
                "201.114.166.24:9001",
                "52.214.94.163:9001",
                "195.176.3.24:8443",
                "37.191.195.28:8443",
                "72.132.134.217:443",
                "104.178.168.242:9001",
                "45.14.233.160:443",
                "217.112.131.98:9001",
                "193.11.164.243:9001",
                "139.162.232.28:9001",
                "82.170.235.23:9111",
                "85.195.235.248:9001",
                "89.163.128.25:9001",
                "51.75.129.204:443",
                "95.154.24.73:9001",
                "185.220.101.12:30012",
                "108.16.117.27:7936",
                "23.105.163.117:443",
                "140.78.100.15:8443",
                "195.206.105.227:13526",
                "91.208.162.203:9001",
                "212.47.254.236:443",
                "51.15.41.39:443",
                "51.158.147.144:443",
                "45.125.166.58:443",
                "45.153.160.130:9001",
                "195.123.212.228:9001",
                "198.98.57.207:443",
                "104.238.167.111:443",
                "83.97.87.182:9051",
                "195.154.243.182:443",
                "91.160.55.21:9001",
                "94.142.241.194:9004",
                "144.91.125.239:9001",
                "94.23.194.134:443",
                "198.50.128.237:9001",
                "80.208.150.185:9001",
                "192.36.38.33:443",
                "107.174.244.102:9001",
                "82.212.170.79:9001",
                "2.29.35.45:9001",
                "198.255.21.2:443",
                "198.100.148.106:443",
                "51.81.56.229:443",
                "178.17.174.198:443",
                "104.200.20.46:9001",
                "150.136.244.162:9000",
                "192.160.102.165:9001",
                "37.221.193.44:9001",
                "45.91.101.18:9001",
                "178.162.194.210:80",
                "185.173.179.18:443",
                "93.115.95.38:443",
                "68.14.177.196:9001",
                "65.21.94.13:9001",
                "46.38.254.223:587",
                "185.220.101.7:20007",
                "104.244.73.126:443",
                "179.43.160.164:9001",
                "91.245.255.4:443",
                "171.25.193.20:443",
                "185.38.175.71:443",
                "188.68.46.245:9001",
                "176.123.7.102:443",
                "45.62.210.72:9001",
                "37.252.188.180:443",
                "83.243.68.194:49005",
                "83.212.102.114:29950",
                "37.218.242.84:8443",
                "37.191.195.28:44100",
                "178.170.10.3:443",
                "81.6.43.252:9001",
                "193.108.117.209:443",
                "92.243.0.63:9001",
                "37.187.23.232:80",
                "91.132.145.245:9001",
                "31.209.7.91:9001",
                "95.216.118.16:4223",
                "79.124.7.11:443",
                "173.249.57.253:433",
                "138.197.171.88:9001",
                "88.198.112.25:9001",
                "81.17.60.24:9001",
                "185.233.107.110:9030",
                "37.191.195.67:38443",
                "193.182.144.53:443",
                "51.255.106.85:443",
                "51.159.139.61:443",
                "71.166.42.70:443",
                "95.216.19.207:21",
                "185.86.150.133:443",
                "213.239.217.68:4433",
                "51.178.17.128:9001",
                "212.47.236.95:443",
                "114.23.164.80:9001",
                "179.43.156.214:443",
                "142.4.215.43:443",
                "87.168.177.66:9001",
                "109.241.167.138:9001",
                "198.180.150.9:9001",
                "195.154.241.145:443",
                "178.62.251.184:9001",
                "101.100.146.147:443",
                "172.245.21.215:80",
                "163.172.169.253:9001",
                "179.43.169.20:443",
                "109.70.100.11:443",
                "140.78.100.19:8443",
                "185.117.118.15:9001",
                "199.195.251.84:9001",
                "157.131.206.89:443",
                "94.226.6.98:9443",
                "23.129.64.164:443",
                "98.217.124.239:9001",
                "178.17.174.239:9001",
                "51.15.246.170:443",
                "185.220.101.19:10019",
                "192.34.58.232:9004",
                "162.212.158.82:9201",
                "135.125.55.228:443",
                "185.220.100.241:9000"
            };

        static internal IPEndPoint GetRandomFallbackDirectory()
        {
            return
                IPEndPoint.Parse (
                    fallbackDirectories
                        .OrderBy(x => Guid.NewGuid())
                        .First()
                );
        }
    }
}
