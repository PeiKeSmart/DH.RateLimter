using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

using NewLife.Caching;

namespace DH.RateLimter;

public static class Common
{
    public const String HeaderStatusKey = "Api-Throttle-Status";
    public const String GlobalApiKey = "global";

    /// <summary>MD5 计算结果缓存，5分钟过期</summary>
    private static readonly MemoryCache _md5Cache = new();

    /// <summary>IP 转数字缓存，5分钟过期</summary>
    private static readonly MemoryCache _ipNumCache = new();

    /// <summary>缓存过期时间（秒）</summary>
    private const Int32 CacheExpireSeconds = 300;

    /// <summary>将 IP 地址转换为数字字符串（带缓存）</summary>
    public static String IpToNum(String ip)
    {
        if (String.IsNullOrEmpty(ip)) return ip;

        return _ipNumCache.GetOrAdd(ip, k => ComputeIpToNum(k), CacheExpireSeconds);
    }

    /// <summary>计算 IP 转数字</summary>
    private static String ComputeIpToNum(String ip)
    {
        var ipAddr = IPAddress.Parse(ip);
        var ipFormat = ipAddr.GetAddressBytes().ToList();
        ipFormat.Reverse();
        ipFormat.Add(0);
        var ipAsInt = new BigInteger(ipFormat.ToArray());
        return ipAsInt.ToString();
    }

    /// <summary>计算 MD5 完整值（带缓存）</summary>
    public static String EncryptMD5(String value)
    {
        if (String.IsNullOrEmpty(value)) return value;

        return _md5Cache.GetOrAdd($"full:{value}", k => ComputeMD5(value), CacheExpireSeconds);
    }

    /// <summary>计算 MD5 短值（带缓存）</summary>
    public static String EncryptMD5Short(String value)
    {
        if (String.IsNullOrEmpty(value)) return value;

        return _md5Cache.GetOrAdd($"short:{value}", k => ComputeMD5(value).Substring(8, 16), CacheExpireSeconds);
    }

    /// <summary>计算 MD5</summary>
    private static String ComputeMD5(String value)
    {
        using var md5Hash = MD5.Create();
        var data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(value));
        var sb = new StringBuilder(32);
        for (var i = 0; i < data.Length; i++)
        {
            sb.Append(data[i].ToString("x2"));
        }
        return sb.ToString();
    }
}
